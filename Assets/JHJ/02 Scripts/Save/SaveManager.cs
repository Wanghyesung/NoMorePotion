using UnityEngine;
using UnityEngine.InputSystem;
using SF = UnityEngine.SerializeField;

/// <summary>
/// 현 상태를 모아 현재 슬롯에 저장함. 인 게임씬에 하나
/// 불러온 값의 복원 순서는 GameSessionManager가 맡음
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // [임시] 테스트용으로만 사용
    [Header("디버그")]
    [SF] private bool enableDebug = true;
    [SF] private int debugSlot = 0;
    [SF] private bool debugLoadOnStart = true;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SaveManager] 인스턴스 중복, 삭제");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 타이틀 안거치고 씬 바로 실행한 경우에만
        if (enableDebug && !SaveSession.HasSlot())
            DebugBeginSession();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SaveSession.Clear();
        }
    }

    private void Update()
    {
        if (!enableDebug || Keyboard.current == null)
            return;

        if (Keyboard.current.f5Key.wasPressedThisFrame)
            SaveNow();
    }

    /// <summary>
    /// 현 상태를 현재 슬롯에 저장(덮어쓰기) 성공 시 true
    /// </summary>
    public bool SaveNow()
    {
        if(!SaveSession.HasSlot())
        {
            Debug.LogWarning("[SaveManager] 슬롯 없음, 저장 안 함");
            return false;
        }

        if (!GameAuthority.HasAuthority)
            return false;

        if(GameSessionManager.Instance == null || GameSessionManager.Instance.Phase.CurrentValue != SessionPhase.Playing)
        {
            Debug.Log("[SaveManager] 진행 중이 아님, 저장 안 함");
            return false;
        }

        SaveData data = Capture();
        if (data == null)
            return false;

        bool ok = SaveStore.Save(SaveSession.Slot, data);

        Debug.Log($"[SaveManager] {SaveSession.Slot}번 슬롯 저장 {(ok ? "성공" : "실패")} : {data.Clock.DayCount}일차 {data.Clock.Phase}");
        return ok;
    }


    private SaveData Capture()
    {
        TimeState clock = TimeManager.Instance != null ? TimeManager.Instance.CaptureState() : null;

        if(clock == null)
        {
            Debug.LogWarning("[SaveManager] 시계가 시작 전, 저장 안 함");
            return null;
        }

        return new SaveData { Clock = clock };
    }

    private void DebugBeginSession()
    {
        SaveData loaded = null;
        if (debugLoadOnStart)
            SaveStore.TryLoad(debugSlot, out loaded);

        SaveSession.Begin(debugSlot, loaded);
        Debug.Log($"[SaveManager] 디버그 슬롯 {debugSlot} 사용, {(loaded != null ? "불러오기" : "새 게임")}");
    }
}
