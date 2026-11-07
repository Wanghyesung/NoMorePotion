using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

/// <summary>
/// 한 판의 진행 단계 관리 (준비 → 플레이 → 끝).
/// 시작 순서와 끝 판정·기록만 맡는다. 다른 매니저는 "시작 함수"만 호출하고,
/// 스폰·불러오기·승패 조건의 구현은 각 담당. 앱 흐름·인게임 나가기는 NetworkFlowManager.
/// </summary>
/// <remarks>
/// [보고] GameSessionManager.Instance.ReportClear() / ReportDefeat()
///   판정 권한(GameAuthority)이 있고 Playing 단계일 때만 반영. 먼저 온 보고만 유효
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
            Debug.LogWarning("[GameSeesionManager] 인스턴스 중복, 삭제");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        phase?.Dispose();
        rankingSubmit?.Dispose();
    }

    private void Start()
    {
        RunStartSequence().Forget();
    }


    /// <summary>
    /// 한 판의 시작 순서. 순서가 곧 규칙이므로 다른 매니저의 시작 함수를 여기서 직접 호출한다.
    /// (끝날 때는 순서가 중요하지 않아 각자 Phase를 구독)
    /// </summary>
    private async UniTaskVoid RunStartSequence()
    {
        // 다른 오브젝트의 Start(구독 등록)가 끝난 뒤 진행하기 위해 한 프레임 대기
        // 대기 중 파괴되면(타이틀로 나감) 이후 단계는 실행되지 않음
        var token = this.GetCancellationTokenOnDestroy();
        await UniTask.NextFrame(token);

        // TODO: 불러오기면 저장값 복원
        // TODO: 플레이어 스폰 (플레이어 담당 스폰 함수)
        // TODO(멀티): 전원 씬 로드 완료 대기

        if (TimeManager.Instance != null)
            TimeManager.Instance.StartClock();
        else
            Debug.LogWarning("[GameSessionManager] 타임매니저 없음");

        ChangePhase(SessionPhase.Preparing, SessionPhase.Playing);
    }

    public void ReportClear() => Finish(GameResult.Clear);

    public void ReportDefeat() => Finish(GameResult.Defeat);


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

        // 순서 중요: 기록 → 단계 변경. 구독자는 Ended를 받는 순간 Record를 읽으므로 기록이 먼저 있어야 함
        record = CreateRecord(gameResult);
        ChangePhase(SessionPhase.Playing, SessionPhase.Ended);

        Debug.Log($"[GameSessionManager] 게임 종료 : {gameResult} ({record.Value.DayCount}일차, {record.Value.PlayTime:F1}초)");

        // TODO(온라인) : 등록이 비동기가 되면 Pending → 완료 순으로 값 갱신
        rankingSubmit.Value = RankingService.TrySubmit(record.Value);
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


    // 판정 순간의 시간 값을 고정. 시계는 Ended를 받고 멈추지만, 그 전에 읽어도 같은 프레임이라 값이 같음
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
}
