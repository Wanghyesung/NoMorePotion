using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Photon.Pun;
using R3;
using System;
using System.Threading;
using UnityEngine;

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
/// [사망·부활] 로컬 플레이어의 사망 이벤트를 구독 (스폰 직후 SubscribeLocalPlayer 참고)
///   사망 위치에 시체 상자(개인 인벤 → 상자) → respawnDelaySeconds 뒤 스폰 지점에서 부활
///
/// [구독] 알림은 Phase 하나뿐. 끝났을 때 기록이 필요하면 Ended를 받은 뒤 Record를 읽는다
///   GameSessionManager.Instance.Phase
///       .Where(p => p == SessionPhase.Ended)
///       .Subscribe(_ => { var rec = GameSessionManager.Instance.Record.Value; ... })
///       .AddTo(this);
///
/// [읽는 값] Record(끝나기 전 null), Result(끝나기 전 None), IsEnded
/// [단계] Preparing → Playing → Ended, 한 방향으로만. 전환은 ChangePhase만
/// </remarks>
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [SerializeField] private Player playerPrefab;
    [SerializeField] private PlayerUIBinding playerBinder;
    [SerializeField] private Transform spawnPoint;
    private GameObject spawnedPlayer;
    private Player localPlayer;

    [Header("수정구슬")]
    // TODO(수정구슬): 수정구슬 클래스가 생기면 타입을 바꿔서 연결
    //   [SerializeField] private Crystal crystal;

    [Header("사망·부활")]
    // 사망 후 스폰 지점에서 부활하기까지의 실제 시간(초). 게임 시간 배율·판정 권한과 무관하게 각자 클라이언트에서 진행
    [SerializeField, Min(0f)] private float respawnDelaySeconds = 5f;
    // 내 플레이어가 사망 처리 중인지 (중복 보고 방지)
    private bool isLocalPlayerDead;

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
        if (playerPrefab != null && PhotonNetwork.PrefabPool is DefaultPool pool)
        {
            pool.ResourceCache[playerPrefab.name] = playerPrefab.gameObject;
        }

    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        UnsubscribeCrystal();
        UnsubscribeLocalPlayer();

        phase?.Dispose();
        rankingSubmit?.Dispose();
    }

    private void Start()
    {
        SubscribeCrystal();
        RunStartSequence().Forget();
    }


    /// <summary>
    /// 한 판의 시작 순서. 순서가 곧 규칙이므로 다른 매니저의 시작 함수를 여기서 직접 호출한다.
    /// (끝날 때 시계 정지도 Finish에서 직접 호출. 그 외 반응(결과창 등)은 각자 Phase를 구독)
    /// </summary>
    private async UniTaskVoid RunStartSequence()
    {
        // 다른 오브젝트의 Start(구독 등록)가 끝난 뒤 진행하기 위해 한 프레임 대기
        // 대기 중 파괴되면(타이틀로 나감) 이후 단계는 실행되지 않음
        var token = this.GetCancellationTokenOnDestroy();
        await UniTask.NextFrame(token);

        // TODO: 불러오기면 저장값 복원
        // 플레이어 프리팹을 지정한 씬에서만 네트워크 플레이어를 생성한다.
        if (playerPrefab != null && SpawnLocalPlayer() == false)
            return;
        // TODO(멀티): 전원 씬 로드 완료 대기

        if (TimeManager.Instance != null)
            TimeManager.Instance.StartClock();
        else
            Debug.LogWarning("[GameSessionManager] 타임매니저 없음");

        ChangePhase(SessionPhase.Preparing, SessionPhase.Playing);
    }

    public void ReportClear() => Finish(GameResult.Clear);

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

        localPlayer = spawnedPlayer.GetComponent<Player>();
        playerBinder.SetPlayer(localPlayer);
        SubscribeLocalPlayer();
        return true;
    }

    public void ReportDefeat() => Finish(GameResult.Defeat);

    #region 수정구슬

    /// <summary>
    /// 수정구슬의 파괴 이벤트를 구독한다. (수정구슬이 Invoke → OnCrystalDestroyed)
    /// </summary>
    private void SubscribeCrystal()
    {
        // TODO(수정구슬): 수정구슬 담당이 만든 이벤트 이름으로 연결
        //   if (crystal != null) crystal.OnDestroyed += OnCrystalDestroyed;
    }

    private void UnsubscribeCrystal()
    {
        // TODO(수정구슬): SubscribeCrystal 과 짝으로 해제
        //   if (crystal != null) crystal.OnDestroyed -= OnCrystalDestroyed;
    }

    /// <summary>
    /// 수정구슬이 파괴됐을 때 (패배). 수정구슬 이벤트의 구독 함수
    /// </summary>
    private void OnCrystalDestroyed()
    {
        Debug.Log("[GameSessionManager] 수정구슬 파괴");
        ReportDefeat();
    }

    #endregion


    #region 사망·부활

    /// <summary>
    /// 내 플레이어의 사망 이벤트를 구독한다. 스폰 직후 한 번. (부활 시 같은 오브젝트를 재사용하므로 다시 구독하지 않음)
    /// 사망 이벤트는 IsLocalPlayer 인 클라이언트에서만 발생하므로 자기 플레이어만 구독하면 된다.
    /// </summary>
    private void SubscribeLocalPlayer()
    {
        // TODO(플레이어): 플레이어 담당이 만든 사망 이벤트 이름으로 연결 (사망 위치 전달)
        //   if (localPlayer != null) localPlayer.OnDied += OnLocalPlayerDied;
    }

    private void UnsubscribeLocalPlayer()
    {
        // TODO(플레이어): SubscribeLocalPlayer 와 짝으로 해제
        //   if (localPlayer != null) localPlayer.OnDied -= OnLocalPlayerDied;
    }

    /// <summary>
    /// 내 플레이어가 죽었을 때 (플레이어 사망 이벤트의 구독 함수).
    /// 사망 위치에 시체 상자를 남기고, respawnDelaySeconds 뒤 스폰 지점에서 부활시킨다.
    /// </summary>
    private void OnLocalPlayerDied(Vector3 deathPosition)
    {
        if (phase.CurrentValue != SessionPhase.Playing)
        {
            Debug.Log($"[GameSessionManager] 진행중 아님, 사망 보고 무시 : 현재 단계 = {phase.CurrentValue}");
            return;
        }

        if (isLocalPlayerDead)
            return;

        isLocalPlayerDead = true;
        Debug.Log($"[GameSessionManager] 내 플레이어 사망 : {deathPosition}");

        DropCorpseBoxAsync(deathPosition).Forget();
        RespawnAfterDelayAsync().Forget();
    }

    /// <summary>
    /// 개인 인벤토리를 사망 위치의 시체 상자로 옮긴다. (마스터가 검증 후 전원에 적용)
    /// 상자를 월드에 보이게 하는 건 TeamInventoryState.OnCorpseCreated / OnCorpseRemoved 를 구독하는 쪽의 몫.
    /// </summary>
    private async UniTaskVoid DropCorpseBoxAsync(Vector3 deathPosition)
    {
        InventoryNetworkSyncManager inventory = InventoryNetworkSyncManager.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[GameSessionManager] InventoryNetworkSyncManager 없음, 시체 상자 생략");
            return;
        }

        InventoryResult result = await inventory.DieAsync(deathPosition);

        // 개인 인벤이 비어 있으면 Invalid 로 거부됨 (남길 것이 없음) → 상자 없이 진행
        if (result.Success)
            Debug.Log($"[GameSessionManager] 시체 상자 생성 : ID {result.Outcome.CorpseId}");
        else
            Debug.Log($"[GameSessionManager] 시체 상자 생성 안 됨 : {result.Reason}");
    }

    private async UniTaskVoid RespawnAfterDelayAsync()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(respawnDelaySeconds), cancellationToken: token)
            .SuppressCancellationThrow();

        // 대기 중 파괴(타이틀로 나감) 또는 판이 끝났으면 부활하지 않음
        if (cancelled || phase.CurrentValue != SessionPhase.Playing)
            return;

        RespawnLocalPlayer();
    }

    /// <summary>
    /// 내 플레이어를 스폰 지점으로 옮기고 되살린다.
    /// </summary>
    private void RespawnLocalPlayer()
    {
        isLocalPlayerDead = false;

        if (spawnedPlayer == null)
        {
            Debug.LogWarning("[GameSessionManager] 부활시킬 플레이어 없음");
            return;
        }

        Transform point = spawnPoint != null ? spawnPoint : transform;
        spawnedPlayer.transform.SetPositionAndRotation(point.position, point.rotation);

        // TODO(플레이어): 플레이어 쪽 부활 함수 연결 (HP 복구, 래그돌 해제, 상태 잠금 해제 → Idle)
        //   if (localPlayer != null) localPlayer.Revive();

        Debug.Log($"[GameSessionManager] 내 플레이어 부활 : {point.position}");
    }

    #endregion


    private void Finish(GameResult gameResult)
    {
        // TODO(멀티): 방장 외 클라이언트에 결과 전달 필요
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

        SubmitRankingAsync(record.Value).Forget();
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


    /// <summary>
    /// 단계 전환은 반드시 여기를 거친다. 현재 단계가 from이 아니면 거부
    /// → 역방향(끝난 뒤 다시 시작)·건너뛰기(준비 중 바로 끝)가 불가능
    /// </summary>
    private bool ChangePhase(SessionPhase from, SessionPhase to)
    {
        if(phase.CurrentValue != from)
        {
            Debug.LogWarning($"[GameSessionManager] 페이즈 변경 불가, 현재 페이즈 = {phase.CurrentValue}");
            return false;
        }

        phase.Value = to;

        Debug.Log($"[GameSessionManager] 페이즈 변경 성공, 현재 페이즈 = {phase.CurrentValue}");
        return true;
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


    [ContextMenu("디버그 클리어")]
    private void DebugClear() => ReportClear();

    [ContextMenu("디버그 패배")]
    private void DebugDefeat() => ReportDefeat();

    [ContextMenu("디버그 수정구슬 파괴")]
    private void DebugCrystalDestroyed() => OnCrystalDestroyed();

    [ContextMenu("디버그 내 플레이어 사망")]
    private void DebugLocalPlayerDied()
    {
        Vector3 position = spawnedPlayer != null ? spawnedPlayer.transform.position : transform.position;
        OnLocalPlayerDied(position);
    }
}
