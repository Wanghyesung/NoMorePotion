using Cysharp.Threading.Tasks;
using Photon.Pun;
using R3;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

/// <summary>
/// 한 판의 진행 단계 관리 (준비 → 플레이 → 끝).
/// 시작 순서와 끝 판정·기록만 맡는다. 다른 매니저는 "시작·정지 함수"만 호출하고,
/// 스폰·불러오기·승패 조건의 구현은 각 담당. 앱 흐름·인게임 나가기는 NetworkFlowManager.
/// 의존은 한 방향 (세션 → TimeManager). TimeManager는 세션을 모른다.
/// </summary>
/// <remarks>
/// [보고] GameSessionManager.Instance.ReportClear() / ReportDefeat()
///   판정 권한(GameAuthority)이 있고 Playing 단계일 때만 반영. 먼저 온 보고만 유효
///   수정구슬 파괴 → 수정구슬이 파괴 이벤트를 Invoke → 세션이 구독해서 패배 처리 (SubscribeCrystal 참고)
///
/// [구독] 알림은 Phase 하나뿐. 끝났을 때 기록이 필요하면 Ended를 받은 뒤 Record를 읽는다
///   GameSessionManager.Instance.Phase
///       .Where(p => p == SessionPhase.Ended)
///       .Subscribe(_ => { var rec = GameSessionManager.Instance.Record.Value; ... })
///       .AddTo(this);
///
/// [읽는 값] Record(끝나기 전 null), Result(끝나기 전 None), IsEnded
/// [단계] Preparing → Playing → Ended, 한 방향으로만. 전환은 ChangePhase만
///   멀티: 단계는 방장이 정하고 클라이언트는 RPC로 따라감 (스스로 바꾸지 않음)
///   시작: 각자 로드 → PlayerProps.LOADED → 방장이 전원 로드(최대 loadTimeout초) 대기 → StartClock → RpcStart
///   늦게 로드된 클라이언트: RpcRequestPhase로 현재 단계 요청 (Playing → RpcStart / Ended → RpcApplyEnded)
///   입력은 Playing 진입(EnterPlaying)에서만 켜짐
/// </remarks>
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Header("플레이어 스폰")]
    [SerializeField] private Player playerPrefab;
    [SerializeField] private PlayerUIBinding playerBinder;
    [SerializeField] private Transform spawnPoint;
    private GameObject spawnedPlayer;
    // [임시] CBT용 죽으면 게임종료
    private Player localPlayer;

    [SerializeField] private float loadTimeout = 30f;   // 로드 대기 최대 시간

    private PhotonView view;
    private ReactiveProperty<SessionPhase> phase = new ReactiveProperty<SessionPhase>(SessionPhase.Preparing);
    private GameRecord? record;
    private ReactiveProperty<RankingSubmitResult> rankingSubmit = new ReactiveProperty<RankingSubmitResult>(RankingSubmitResult.None);
    


    /// <summary> 현재 진행 단계. 구독 즉시 현재 값 받음. 알림은 이것 하나 </summary>
    public ReadOnlyReactiveProperty<SessionPhase> Phase => phase;
    /// <summary> 판 기록. Ended 이후에만 값 있음 (Ended 알림 시점엔 이미 채워져 있음) </summary>
    public GameRecord? Record => record;
    /// <summary> 판 결과. 끝나기 전에는 None </summary>
    public GameResult Result => record.HasValue ? record.Value.Result : GameResult.None;
    /// <summary> 랭킹 등록 결과. 등록 전에는 None </summary>
    public ReadOnlyReactiveProperty<RankingSubmitResult> RankingSubmit => rankingSubmit;
    /// <summary> 판이 끝났는지 (구독 없이 확인용) </summary>
    public bool IsEnded => Phase.CurrentValue == SessionPhase.Ended;


    [Header("디버그")]
    [SerializeField] private bool enableDebugKeys = true;   // 정식 빌드 전에 끄기


    #region 생명주기

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameSessionManager] 인스턴스 중복, 삭제");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Resources 밖의 프리팹도 이름으로 생성할 수 있도록 모든 클라이언트에서 등록한다.
        if (playerPrefab != null)
            PhotonPoolAdapter.Register(playerPrefab.gameObject);

        view = gameObject.GetComponent<PhotonView>();
        if (view == null)
            Debug.LogWarning("[GameSessionManager] PhotonView 없음 -> 멀티 동기화 불가", this);
    }

    private void Start()
    {
        SubscribeCrystal();
        RunStartSequence().Forget();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeCrystal();
        UnsubscribePlayer();

        phase?.Dispose();
        rankingSubmit?.Dispose();
    }

    private void Update()
    {
        if (!enableDebugKeys || Keyboard.current == null)
            return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
            ReportClear();
        else if (Keyboard.current.f2Key.wasPressedThisFrame)
            ReportDefeat();  
    }

    #endregion


    #region 시작

    /// <summary>
    /// 한 판의 시작 순서. 순서가 곧 규칙이므로 다른 매니저의 시작 함수를 여기서 직접 호출한다.
    /// (끝날 때 시계 정지도 Finish에서 직접 호출. 그 외 반응(결과창 등)은 각자 Phase를 구독)
    /// </summary>
    private async UniTaskVoid RunStartSequence()
    {
        // 플레이어 입력 막기
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputEnabled(false);

        // 다른 오브젝트의 Start(구독 등록)가 끝난 뒤 진행하기 위해 한 프레임 대기
        // 대기 중 파괴되면(타이틀로 나감) 이후 단계는 실행되지 않음
        var token = this.GetCancellationTokenOnDestroy();
        await UniTask.NextFrame(token);

        if (IsEnded) return;

        // TODO: 불러오기면 저장값 복원


        // 플레이어 프리팹을 지정한 씬에서만 네트워크 플레이어를 생성한다.
        if (playerPrefab != null && SpawnLocalPlayer() == false)
            return;

        MarkLoaded();

        if (GameAuthority.HasAuthority)
        {
            float deadline = Time.realtimeSinceStartup + loadTimeout;

            await UniTask.WaitUntil(() => AllLoaded() || Time.realtimeSinceStartup >= deadline, cancellationToken: token);

            if (!AllLoaded())
                Debug.LogWarning($"[GameSessionManager] 로드 대기 {loadTimeout}초 초과, 로드 안 된 플레이어 제외하고 시작 ");

            if (TimeManager.Instance != null)
                TimeManager.Instance.StartClock();
            else
                Debug.LogWarning("[GameSessionManager] 타임매니저 없음");

            EnterPlaying();

            if (CanSend())
                view.RPC(nameof(RpcStart), RpcTarget.Others);
        }

        else
            view.RPC(nameof(RpcRequestPhase), RpcTarget.MasterClient);
    }

    private bool SpawnLocalPlayer()
    {
        if (spawnedPlayer != null)
            return true;

        if (PhotonNetwork.InRoom == false || playerBinder == null)
        {
            Debug.LogError("[GameSessionManager] 방 입장 완료와 Player Binder 연결이 필요합니다.", this);
            return false;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        spawnedPlayer = PhotonNetwork.Instantiate(playerPrefab.name, point.position, point.rotation);
        if (spawnedPlayer == null)
            return false;

        // [임시] CBT용
        localPlayer = spawnedPlayer.GetComponent<Player>();
        SubscribePlayer();
        playerBinder.SetPlayer(localPlayer);

        return true;
    }

    #endregion

    #region 로딩
    private void MarkLoaded()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PlayerProps.LOADED, true } });
    }

    private bool AllLoaded()
    {
        foreach(PhotonPlayer p in PhotonNetwork.PlayerList)
        {
            if (!PlayerProps.IsLoaded(p))
                return false;
        }

        return true;
    }

    private void EnterPlaying()
    {
        if (!ChangePhase(SessionPhase.Preparing, SessionPhase.Playing))
            return;

        // 페이즈 스타트되면 커서 잠금, 입력켜기
        if (InputManager.Instance != null)
        {
            InputManager.Instance.SetInputEnabled(true);
            InputManager.Instance.SetCursorLocked(true);
        }
    }
    

    #endregion

    #region [임시] 플레이어 죽으면 게임 종료되도록. cbt용

    private void SubscribePlayer()
    {
        if (localPlayer != null)
            localPlayer.OnDead += OnPlayerDead;
    }

    private void UnsubscribePlayer()
    {
        if (localPlayer != null)
            localPlayer.OnDead -= OnPlayerDead;
    }

    private void OnPlayerDead(Vector3 position) => ReportDefeat();

    #endregion

    #region 수정구슬

    /// <summary>
    /// 수정구슬의 파괴 이벤트를 구독한다. (수정구슬이 Invoke → OnCrystalDestroyed)
    /// </summary>
    private void SubscribeCrystal() => CrystalCore.OnBroken += OnCrystalDestroyed;
    private void UnsubscribeCrystal() => CrystalCore.OnBroken -= OnCrystalDestroyed;

    /// <summary>
    /// 수정구슬이 파괴됐을 때 (패배). 수정구슬 이벤트의 구독 함수
    /// </summary>
    private void OnCrystalDestroyed(CrystalCore crystal) { ReportDefeat(); }

    #endregion


    #region 끝 판정·기록

    public void ReportClear() => Finish(GameResult.Clear);

    public void ReportDefeat() => Finish(GameResult.Defeat);

    private void Finish(GameResult gameResult)
    {
        if (!GameAuthority.HasAuthority)
        {
            Debug.Log($"[GameSessionManager] 판정 권한 없음, 무시 : {gameResult}");
            return;
        }

        // Preparing(시작 전)과 Ended(이미 끝남)를 한 번에 막음
        if (phase.CurrentValue != SessionPhase.Playing)
        {
            Debug.Log($"[GameSessionManager] 진행중 아님, 무시 : 현재 단계 = {phase.CurrentValue}, 결과 = {gameResult}");
            return;
        }

        // 순서 중요: 기록 → 시계 정지 → 단계 변경
        // 기록이 먼저 : 판정 순간의 시간을 고정. 구독자는 Ended를 받는 순간 Record를 읽으므로 기록이 먼저 있어야 함
        // 정지가 단계 변경보다 먼저 : Ended 구독자가 이미 멈춘 시계를 보게 됨
        record = CreateRecord(gameResult);

        if (TimeManager.Instance != null)
            TimeManager.Instance.StopClock();

        ChangePhase(SessionPhase.Playing, SessionPhase.Ended);

        Debug.Log($"[GameSessionManager] 게임 종료 : {gameResult} ({record.Value.DayCount}일차, {record.Value.PlayTime:F1}초)");

        SendEnded(record.Value);
        SubmitRankingAsync(record.Value).Forget();
    }

    // 판정 순간의 시간 값을 고정. 시계는 기록 직후 Finish에서 멈춤
    private GameRecord CreateRecord(GameResult gameResult)
    {
        TimeManager timeManager = TimeManager.Instance;
        if (timeManager == null)
        {
            Debug.LogWarning("[GameSessionManager] TimeManager 없음, 기록 시간이 0으로 남음");
            return new GameRecord(gameResult, 0f, 0);
        }

        return new GameRecord(gameResult, timeManager.TotalElapsed, timeManager.DayCount.CurrentValue);
    }

    private async UniTaskVoid SubmitRankingAsync(GameRecord record)
    {
        // 1. 등록 대상이 아니면 이유만 알리고 끝 (즉시, 서버 안 감)
        if (!RankingService.CanSubmit(record, out RankingSubmitResult reason))
        {
            rankingSubmit.Value = reason;
            return;
        }

        // 2. 결과 창에 등록중 표시
        rankingSubmit.Value = RankingSubmitResult.Pending;

        // 3. 세션 파괴되면 취소되는 토큰
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        // 4. 저장(재시도 포함)
        rankingSubmit.Value = await RankingService.SubmitAsync(record, token);
    }

    #endregion

    #region 네트워크

    [PunRPC]
    private void RpcStart(PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient)
            return;

        EnterPlaying();
    }

    [PunRPC]
    private void RpcRequestPhase(PhotonMessageInfo info)
    {
        if (!GameAuthority.HasAuthority || info.Sender == null)
            return;

        switch(phase.CurrentValue)
        {
            case SessionPhase.Playing:
                view.RPC(nameof(RpcStart), info.Sender);
                return;

            case SessionPhase.Ended:
                SendEnded(record.Value, info.Sender);
                return;

                // 전원 로드 이후 RpcStart 보내기때문에 그때 받음. 여기서는 무시
            case SessionPhase.Preparing:
                return;
        }
    }

    [PunRPC]
    private void RpcApplyEnded(int result, float playtime, int dayCount, PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient)
            return;

        if (phase.CurrentValue == SessionPhase.Ended)
            return;

        record = new GameRecord((GameResult)result, playtime, dayCount);

        ChangePhase(phase.CurrentValue, SessionPhase.Ended);
        rankingSubmit.Value = RankingSubmitResult.NotHost;
        Debug.Log($"[GameSessionManager] 방장 종료 수신 : {(GameResult)result} ({dayCount}일차, {playtime:F1}초)");
    }

    #region 내부

    /// <summary> 방장 → 다른 모든 사람 (Finish에서) </summary>
    private void SendEnded(GameRecord record)
    {
        if (!CanSend()) return;
        view.RPC(nameof(RpcApplyEnded), RpcTarget.Others, BuildEndedArgs(record));
    }

    /// <summary> 방장 → 한 사람 (늦게 로드된 사람의 요청 응답, RpcRequestPhase에서) </summary>
    private void SendEnded(GameRecord record, PhotonPlayer target)
    {
        if (target == null || !CanSend()) return;
        view.RPC(nameof(RpcApplyEnded), target, BuildEndedArgs(record));
    }


    // RpcApplyEnded 인자 순서와 반드시 같아야 함
    private object[] BuildEndedArgs(GameRecord record)
    {
        return new object[] { (int)record.Result, record.PlayTime, record.DayCount };
    }

    private bool CanSend()
    {
        return view != null && PhotonNetwork.InRoom;
    }
    #endregion

    #endregion

    /// <summary>
    /// 단계 전환은 반드시 여기를 거친다. 현재 단계가 from이 아니면 거부
    /// → 역방향(끝난 뒤 다시 시작)·건너뛰기(준비 중 바로 끝)가 불가능 (예외: 클라이언트가 방장 종료를 받을 때, RpcApplyEnded)
    /// </summary>
    private bool ChangePhase(SessionPhase from, SessionPhase to)
    {
        if (phase.CurrentValue != from)
        {
            Debug.LogWarning($"[GameSessionManager] 페이즈 변경 불가, 현재 페이즈 = {phase.CurrentValue}");
            return false;
        }

        phase.Value = to;

        Debug.Log($"[GameSessionManager] 페이즈 변경 성공, 현재 페이즈 = {phase.CurrentValue}");
        return true;
    }


    #region 디버그

    [ContextMenu("디버그 클리어")]
    private void DebugClear() => ReportClear();

    [ContextMenu("디버그 패배")]
    private void DebugDefeat() => ReportDefeat();

    #endregion
}
