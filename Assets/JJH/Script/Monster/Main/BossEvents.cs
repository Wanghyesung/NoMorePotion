using System;
using UnityEngine;

public class BossEvents : MonoBehaviour
{
    public static event Action<NormalMonster, string> OnEngaged;
    public static event Action<NormalMonster> OnDisengaged;

    public static void Engage(NormalMonster data, string name) => OnEngaged?.Invoke(data, name);
    public static void Disengage(NormalMonster data) => OnDisengaged?.Invoke(data);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnEngaged = null;
        OnDisengaged = null;
    }
}
