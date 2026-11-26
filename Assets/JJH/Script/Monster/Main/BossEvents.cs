using System;
using UnityEngine;

public class BossEvents : MonoBehaviour
{
    /// <summary>
    /// 보스 인터페이스 연동
    /// </summary>
    public static event Action<BossMonster, string, Transform> OnEngaged;
    /// <summary>
    /// 보스 인터페이스 연동 해제
    /// </summary>
    public static event Action<BossMonster> OnDisengaged;
    /// <summary>
    /// 보스 패배시 호출
    /// </summary>
    public static event Action<BossMonster> OnDefeated;
    public static void Engage(BossMonster data, string name, Transform owner) =>
        OnEngaged?.Invoke(data, name, owner);
    public static void Disengage(BossMonster data) => OnDisengaged?.Invoke(data);
    public static void Defeat(BossMonster data) => OnDefeated?.Invoke(data);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnEngaged = null;
        OnDisengaged = null;
        OnDefeated = null;
    }
}
