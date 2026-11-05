using UnityEngine;
using Photon.Pun;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [SerializeField] float dayDuration = 10; // [임시] 설정값
    [SerializeField] float nightDuration = 10;

    private TimeState state;
    private bool isRunning;

    private float currentDuration => state.Phase == TimePhase.Day ? dayDuration : nightDuration;


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
    }


    private void Update()
    {
        if (!isRunning) return;
        if (!PhotonNetwork.IsMasterClient) return;

        Tick(Time.deltaTime);
    }


    public void StartClock()
    {
        if (isRunning)
        {
            Debug.Log("[TimeManager] 이미 진행 중, 무시");
            return;
        }

        state = new TimeState { DayCount = 1, Phase = TimePhase.Day, Elapsed = 0 };
        isRunning = true;
        Debug.Log("[TimeManager] 1일차 낮 시작");
    }

    private void Tick(float deltaTime)
    {
        state.Elapsed += deltaTime;
        while (state.Elapsed >= currentDuration)
        {
            state.Elapsed -= currentDuration;
            AdvancePhase();
        }
    }

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
    }

    [ContextMenu("다음 단계로 강제 전환")]
    // 디버그용
    private void ForceNextPhase()
    {
        if (!isRunning) return;

        state.Elapsed = 0;
        AdvancePhase();
    }
}
