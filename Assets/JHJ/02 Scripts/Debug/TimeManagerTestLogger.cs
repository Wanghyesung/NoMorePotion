using UnityEngine;
using R3;

// 테스트용, 커밋하지 않음
public class TimeManagerTestLogger : MonoBehaviour
{
    private void Start()
    {
        TimeManager tm = TimeManager.Instance;
        if (tm == null)
        {
            Debug.LogWarning("[Test] TimeManager 없음");
            return;
        }

        tm.Phase.Subscribe(value => Debug.Log($"[Test] 단계: {value}")).AddTo(this);
        tm.DayCount.Subscribe(value => Debug.Log($"[Test] 일차: {value}")).AddTo(this);
        tm.RemainSec.Subscribe(value => Debug.Log($"[Test] 남은 초: {value}")).AddTo(this);
    }

    [ContextMenu("진행 비율 출력")]
    private void LogProgress()
    {
        if (TimeManager.Instance == null) return;
        Debug.Log($"[Test] 진행 비율: {TimeManager.Instance.PhaseProgress}");
    }
}