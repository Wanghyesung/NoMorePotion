using Photon.Pun;
using R3;
using UnityEngine;


/// <summary>
/// 한 판의 승패 판정과 기록. 앱 종료·인게임 나가기는 NetworkFlowManager 담당.
/// </summary>
/// <remarks>
/// [보고] GameEndManager.Instance.ReportClear() / ReportDefeat()
///   방장에서만 반영, 먼저 온 보고만 유효 (이후 보고는 무시)
///   판정 순간에 기록(GameRecord)을 만들고 랭킹 등록까지 여기서 처리 → 방장 1번만 등록됨
///
/// [구독] using R3; 필요, Start에서 구독
///   GameEndManager.Instance.Result
///       .Where(r => r != GameResult.None)
///       .Subscribe(r => { ... }).AddTo(this);
///   .Skip(1) 대신 Where 사용 → 판이 끝난 뒤에 구독해도 결과를 받음
///
///   Record        : 판 기록(결과, 플레이 타임, 일차). 끝나기 전에는 null
///   Result        : Record에서 결과만 뽑은 값. 따로 저장하지 않으므로 둘이 어긋날 수 없음
///   RankingSubmit : 랭킹 등록 결과. Record보다 늦게 바뀜 (등록이 비동기가 되어도 구독 쪽은 그대로)
///
/// [확인만] IsEnded : 구독 없이 지금 끝났는지 확인
/// </remarks>
public class GameEndManager : MonoBehaviour
{
    public static GameEndManager Instance { get; private set; }

    // 판 상태의 유일한 원본. Result는 여기서 파생 → "기록은 비었는데 결과만 바뀐" 순간이 없음
    private ReactiveProperty<GameRecord?> record = new ReactiveProperty<GameRecord?>(null);
    private ReactiveProperty<RankingSubmitResult> rankingSubmit = new ReactiveProperty<RankingSubmitResult>(RankingSubmitResult.None);
    private ReadOnlyReactiveProperty<GameResult> result;


    /// <summary> 판 기록. 끝나기 전에는 null, 구독 즉시 현재 값 받음 </summary>
    public ReadOnlyReactiveProperty<GameRecord?> Record => record;
    /// <summary> 판 결과. 끝나기 전에는 None, 구독 즉시 현재 값 받음 </summary>
    public ReadOnlyReactiveProperty<GameResult> Result => result;
    /// <summary> 랭킹 등록 결과. 등록 전에는 None </summary>
    public ReadOnlyReactiveProperty<RankingSubmitResult> RankingSubmit => rankingSubmit;
    /// <summary> 판이 끝났는지 (구독 없이 확인용) </summary>
    public bool IsEnded => record.CurrentValue.HasValue;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameEndManager] 인스턴스 중복, 삭제");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        result = record
            .Select(r => r.HasValue ? r.Value.Result : GameResult.None)
            .ToReadOnlyReactiveProperty(GameResult.None);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        result?.Dispose();
        record?.Dispose();
        rankingSubmit?.Dispose();
    }

    public void ReportClear() => Finish(GameResult.Clear);

    public void ReportDefeat() => Finish(GameResult.Defeat);


    private void Finish(GameResult gameResult)
    {
        // TODO(멀티): 방장 외 클라이언트에 결과 전달 필요 (방장이 만든 Record를 그대로 보낼 것, 각자 만들지 말 것)
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log($"[GameEndManager] 방장 아님, 무시 : {gameResult}");
            return;
        }

        if (IsEnded)
        {
            Debug.Log($"[GameEndManager] 이미 종료됨, 무시 : {gameResult}");
            return;
        }

        GameRecord newRecord = CreateRecord(gameResult);

        // 첫 줄에서 IsEnded가 true가 됨 → 구독자 안에서 다시 보고해도 무시됨
        record.Value = newRecord;

        Debug.Log($"[GameEndManager] 게임 종료 : {gameResult} ({newRecord.DayCount}일차, {newRecord.PlayTime:F1}초)");

        // TODO(온라인) : 등록이 비동기가 되면 Pending → 완료 순으로 값 갱신
        rankingSubmit.Value = RankingService.TrySubmit(newRecord);
    }

    // 판정 순간의 시간 값을 고정. 시계는 Result를 받고 멈추지만, 그 전에 읽어도 같은 프레임이라 값이 같음
    private GameRecord CreateRecord(GameResult gameResult)
    {
        TimeManager timeManager = TimeManager.Instance;
        if (timeManager == null)
        {
            Debug.LogWarning("[GameEndManager] TimeManager 없음, 기록 시간이 0으로 남음");
            return new GameRecord(gameResult, 0f, 0);
        }

        return new GameRecord(gameResult, timeManager.TotalElapsed, timeManager.DayCount.CurrentValue);
    }


    [ContextMenu("디버그 클리어")]
    private void DebugClear() => ReportClear();

    [ContextMenu("디버그 패배")]
    private void DebugDefeat() => ReportDefeat();
}
