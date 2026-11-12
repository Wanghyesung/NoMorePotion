using Photon.Pun;
using R3;
using System;
using System.Threading;
using UnityEngine;

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
///   TotalElapsed  : float, 판 전체 경과 시간
///   둘 다 시작 전에는 0, 시계가 멈춘 뒤에는 멈춘 시점 값 유지
///
/// [예약 실행]
///   TimeManager.Instance.Schedule(5f, () => { ... }).AddTo(this);
///   게임 시간(TotalElapsed) 기준, 시계가 도는 동안 판정 권한(GameAuthority)이 있을 때만 실행
///   시계가 멈추면 남은 시간도 멈춤. 내 화면 연출용 대기는 UniTask.Delay 사용
///   취소 토큰을 넘기면 토큰이 취소될 때 예약도 취소됨 (선택)
///     TimeManager.Instance.Schedule(5f, () => { ... }, destroyCancellationToken);                 // 파괴 시 취소
///     TimeManager.Instance.Schedule(5f, () => { ... }, PoolManager.GetReleaseToken(gameObject));  // 풀 오브젝트: 반납·파괴 시 취소
///   [풀 오브젝트 주의] AddTo(this)·destroyCancellationToken은 파괴될 때만 취소됨
///     → 반납돼도 예약이 살아 있으므로 풀 오브젝트는 GetReleaseToken 사용
///
/// [시작·정지] StartClock() / StopClock(). 인게임에서는 GameSessionManager가 호출
///   TimeManager는 세션을 모름 → 세션 없는 씬에서도 직접 호출해 단독 테스트 가능
///   멈춘 뒤 값은 그대로 남음
///
/// [주의] 구독 즉시 현재 값으로 한 번 호출됨.
///   전환 순간에만 실행할 거면 .Skip(1) 붙이기
/// </remarks>
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    // 0이면 PhaseProgress 0 나누기, Tick의 while 무한 루프 → 최솟값 보장
    private const float MIN_DURATION = 0.1f;

    [SerializeField] float dayDuration = 10; // [임시] 설정값
    [SerializeField] float nightDuration = 10;
    [SerializeField] float syncInterval = 2f;   // 보정 간격
    
    private float syncTimer;
    private PhotonView view;
    private TimerScheduler timer = new TimerScheduler();
    // StartClock에서만 생성. null = 시작 전 (미리 생성하지 말 것)
    private TimeState state;
    // 지금 시계가 돌고 있는지. 시작 여부는 state로 판단
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


    /// <summary>
    /// 현재 단계 진행 비율(0~1). 매 프레임 바뀌므로 구독 대신 Update에서 직접 읽는다.
    /// 시작 전에는 0, 멈춘 뒤에는 멈춘 시점 값.
    /// </summary>
    public float PhaseProgress => state != null ? state.Elapsed / currentDuration : 0f;
    /// <summary> 판 시작 후 흐른 게임 시간 총합. 플레이타임용. 시작 전 0, 멈춘 뒤에는 멈춘 시점 값 </summary>
    public float TotalElapsed => state != null ? state.TotalElapsed : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TimeManager] 인스턴스 중복, 삭제");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        view = gameObject.GetComponent<PhotonView>();
        if (view == null)
            Debug.LogWarning("[TimeManager] PhotonView 없음 -> 멀티 동기화 불가", this);
    }

    private void Start()
    {
        if (PhotonNetwork.InRoom && !GameAuthority.HasAuthority)
            view.RPC(nameof(RpcRequestState), RpcTarget.MasterClient);
    }

    private void OnValidate()
    {
        dayDuration = Mathf.Max(dayDuration, MIN_DURATION);
        nightDuration = Mathf.Max(nightDuration, MIN_DURATION);
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
        
        if (GameAuthority.HasAuthority)
            Tick(Time.deltaTime);
        else
            PredictTick(Time.deltaTime);

    }

    /// <summary>시계를 1일차 낮으로 초기화하고 시작한다. 이미 진행 중이면 무시.</summary>
    public void StartClock()
    {
        if (isRunning)
        {
            Debug.Log("[TimeManager] 이미 진행 중, 무시");
            return;
        }

        state = new TimeState { DayCount = 1, Phase = TimePhase.Day, Elapsed = 0, TotalElapsed = 0 };
        isRunning = true;
        SyncPublicValues();

        SendState();
        Debug.Log("[TimeManager] 1일차 낮 시작");
    }

    /// <summary>
    /// 시계를 멈춘다. 값(일차, 단계, 경과 시간)은 그대로 남는다. 돌고 있지 않으면 무시.
    /// 예약 실행(Schedule)도 함께 멈춘다.
    /// </summary>
    public void StopClock()
    {
        if (!isRunning)
        {
            Debug.Log("[TimeManager] 돌고 있지 않음, 무시");
            return;
        }

        isRunning = false;
        SendState();
        Debug.Log($"[TimeManager] 시계 정지 ({state.DayCount}일차 {state.Phase}, 총 {state.TotalElapsed:F1}초)");
    }

    /// <summary>
    /// 게임 시간으로 delay초 뒤 callback 실행 예약. 반환값을 AddTo(this) 하면 오브젝트 파괴 시 자동 취소.
    /// 시계가 도는 동안 판정 권한(GameAuthority)이 있을 때만 실행됨. 시계가 멈춘 동안은 남은 시간도 멈춤.
    /// 시작 전에 예약하면 시작 시점부터 delay초 뒤 실행.
    /// </summary>
    public IDisposable Schedule(float delay, Action callback)
    {
        return timer.Schedule(TotalElapsed, delay, callback);
    }

    /// <summary>
    /// Schedule과 같고, cancellationToken이 취소되면 예약도 취소된다.
    /// 일반 오브젝트: destroyCancellationToken (파괴 시 취소)
    /// 풀 오브젝트:  PoolManager.GetReleaseToken(gameObject) (반납·파괴 시 취소)
    /// </summary>
    public IDisposable Schedule(float delay, Action callback, CancellationToken cancellationToken)
    {
        return timer.Schedule(TotalElapsed, delay, callback, cancellationToken);
    }

    private void Tick(float deltaTime)
    {
        state.Elapsed += deltaTime;
        state.TotalElapsed += deltaTime;

        // 넘친 시간을 다음 단계로 넘겨 오차가 쌓이지 않게
        // while : 프레임 지연이 단계 길이보다 길어도 한 번에 따라잡음
        while (state.Elapsed >= currentDuration)
        {
            state.Elapsed -= currentDuration;
            AdvancePhase();
        }

        remainSec.Value = Mathf.CeilToInt(currentDuration - state.Elapsed);

        timer.Tick(state.TotalElapsed);

        // 주기 보정
        syncTimer += deltaTime;
        if(syncTimer >= syncInterval)
        {
            syncTimer = 0f;
            SendState();
        }
    }

    // MasterClient가 아니라면 시간만 흘리고 낮밤 전환이나 예약실행 없음
    private void PredictTick(float deltaTime)
    {
        // 실제 전환은 방장 메세지가 오면
        state.Elapsed = Mathf.Min(state.Elapsed + deltaTime, currentDuration);
        state.TotalElapsed += deltaTime;
        remainSec.Value = Mathf.CeilToInt(currentDuration - state.Elapsed);
    }

    /// <summary>낮↔밤 전환. 일차는 아침 기준으로 증가. 전환은 반드시 이 함수를 거침</summary>
    private void AdvancePhase()
    {
        if (state.Phase == TimePhase.Day)
            state.Phase = TimePhase.Night;

        else
        {
            state.Phase = TimePhase.Day;
            state.DayCount++;
        }

        Debug.Log($"[TimeManager] {state.DayCount}일차 {state.Phase}");
        SyncPublicValues();
        SendState();
    }

    private void SyncPublicValues()
    {
        phase.Value = state.Phase;
        dayCount.Value = state.DayCount;
        remainSec.Value = Mathf.CeilToInt(currentDuration - state.Elapsed);
    }

    //===================================================================
    #region 네트워크
    private void SendState(RpcTarget target = RpcTarget.Others)
    {
        if (view == null || !PhotonNetwork.InRoom || !GameAuthority.HasAuthority || state == null)
            return;

        view.RPC(nameof(RpcApplyState), target, state.DayCount, (int)state.Phase, state.Elapsed, state.TotalElapsed, isRunning);
    }

    [PunRPC]
    private void RpcApplyState(int day, int phaseValue, float elapsed, float totalElapsed, bool running, PhotonMessageInfo info)
    {
        if (info.Sender == null || !info.Sender.IsMasterClient)
            return;

        // 보내는 동안 흐른 시간 보정
        float lag = running ? Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime)) : 0f;

        state ??= new TimeState(); // state가 null이면
        state.DayCount = day;
        state.Phase = (TimePhase)phaseValue;
        state.Elapsed = Mathf.Min(elapsed + lag, currentDuration);
        state.TotalElapsed = totalElapsed + lag;
        isRunning = running;

        SyncPublicValues(); //같은 값이면 ReactiveProperty가 알림을 안 보내서 중복 실행 없음
    }

    // 마스터보다 늦게 들어온 경우 첫 메세지 요청
    [PunRPC]
    private void RpcRequestState(PhotonMessageInfo info)
    {
        if(!GameAuthority.HasAuthority || info.Sender == null || state == null)
                return;

        view.RPC(nameof(RpcApplyState), info.Sender, state.DayCount, (int)state.Phase, state.Elapsed, state.TotalElapsed, isRunning);
    }

    #endregion
    //===================================================================

    // 디버그용
    [ContextMenu("다음 단계로 강제 전환")]
    private void ForceNextPhase()
    {
        if (!isRunning) return;

        state.Elapsed = 0; // 총 경과 시간은 그대로
        AdvancePhase();
    }
}