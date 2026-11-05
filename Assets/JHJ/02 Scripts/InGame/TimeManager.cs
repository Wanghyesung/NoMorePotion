using UnityEngine;
using Photon.Pun;
using R3;

/// <summary>
/// 인게임 내 낮/밤 순환 및 일차 관리
/// </summary>
/// <remarks>
/// [구독 방법] using R3; 필요, Start에서 구독
///   TimeManager.Instance.Phase.Subscribe(phase => { ... }).AddTo(this);
///
/// [구독 값]
///   Phase     : TimePhase (Day/Night), 낮밤 전환 시
///   DayCount  : int, 아침이 올 때
///   RemainSec : int, 남은 초(올림), 1초마다
///
/// [직접 읽는 값]
///   PhaseProgress : float (0~1), Update에서 읽기
///
/// [주의] 구독 즉시 현재 값으로 한 번 호출됨.
///   전환 순간에만 실행할 거면 .Skip(1) 붙이기
/// </remarks>
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [SerializeField] float dayDuration = 10; // [임시] 설정값
    [SerializeField] float nightDuration = 10;

    private TimeState state;
    private bool isRunning;
    // 저장하지 않고 읽을 때마다 계산 → 단계가 바뀌면 자동으로 다음 단계 길이가 됨
    private float currentDuration => state.Phase == TimePhase.Day ? dayDuration : nightDuration;
    // 내부 state와 별도 저장소. state가 바뀌면 SyncPublicValues로 맞춤
    private ReactiveProperty<TimePhase> phase = new ReactiveProperty<TimePhase>(TimePhase.Day);
    private ReactiveProperty<int> dayCount = new ReactiveProperty<int>(1);
    private ReactiveProperty<int> remainSec = new ReactiveProperty<int>(0);

    /// <summary> 현재 낮/밤, 구독 즉시 현재 값 받음 </summary>
    public ReadOnlyReactiveProperty<TimePhase> Phase => phase;
    /// <summary> 며칠째인지, 아침마다 증가 </summary>
    public ReadOnlyReactiveProperty<int> DayCount => dayCount;
    /// <summary> 다음단계까지 남은 초, 값이 바뀔때만 알림 </summary>
    public ReadOnlyReactiveProperty<int> RemainSec => remainSec;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Debug.LogWarning("[TimeManager] 인스턴스 중복, 삭제");
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
        dayCount?.Dispose();
        remainSec?.Dispose();
    }


    private void Update()
    {
        if (!isRunning) return;
        // TODO(멀티): 방장만 진행하므로 다른 클라이언트는 값이 갱신되지 않음. 동기화 어떻게할지
        if (!PhotonNetwork.IsMasterClient) return;

        Tick(Time.deltaTime);
    }

    /// <summary>시계를 1일차 낮으로 초기화하고 시작한다. 이미 진행 중이면 무시.</summary>
    public void StartClock()
    {
        if (isRunning)
        {
            Debug.Log("[TimeManager] 이미 진행 중, 무시");
            return;
        }

        state = new TimeState { DayCount = 1, Phase = TimePhase.Day, Elapsed = 0 };
        isRunning = true;
        SyncPublicValues();

        Debug.Log("[TimeManager] 1일차 낮 시작");

    }

    private void Tick(float deltaTime)
    {
        state.Elapsed += deltaTime;

        // 넘친 시간을 다음 단계로 넘겨 오차가 쌓이지 않게
        if (state.Elapsed >= currentDuration)
        {
            state.Elapsed -= currentDuration;
            AdvancePhase();
        }

        remainSec.Value = Mathf.CeilToInt(currentDuration - state.Elapsed);
    }

    /// <summary>낮↔밤 전환. 일차는 아침 기준으로 증가. 전환은 반드시 이 함수를 거침</summary>
    private void AdvancePhase()
    {
        if(state.Phase == TimePhase.Day)
            state.Phase = TimePhase.Night;

        else
        {
            state.Phase = TimePhase.Day;
            state.DayCount++;
        }

        Debug.Log($"[TimeManager] {state.DayCount}일차 {state.Phase}");
        SyncPublicValues();
    }

    private void SyncPublicValues()
    {
        phase.Value = state.Phase;
        dayCount.Value = state.DayCount;
        remainSec.Value = Mathf.CeilToInt(currentDuration - state.Elapsed);
    }

    // 디버그용
    [ContextMenu("다음 단계로 강제 전환")]
    private void ForceNextPhase()
    {
        if (!isRunning) return;

        state.Elapsed = 0;
        AdvancePhase();
    }
}
