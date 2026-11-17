using System.Collections.Generic;
using UnityEngine;

public class WaveMonsterRegistry
{
    private static readonly List<WaveMonsterMember> monsters = new();
    public static IReadOnlyList<WaveMonsterMember> Monsters => monsters;

    public static void Register(WaveMonsterMember m)
    {
        if (!monsters.Contains(m)) monsters.Add(m);
    }

    public static void Unregister(WaveMonsterMember m) => monsters.Remove(m);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => monsters.Clear();
}
