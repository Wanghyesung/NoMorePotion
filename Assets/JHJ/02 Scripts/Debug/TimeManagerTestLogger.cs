using UnityEngine;
using R3;

// 테스트용 : TimeManager 구독 값을 콘솔에 출력 (TestInGameScene에서 사용 중)
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

}