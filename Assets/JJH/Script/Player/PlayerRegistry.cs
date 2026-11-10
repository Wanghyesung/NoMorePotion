using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 씬에 존재하는 플레이어 목록 (FindGameObjectsWithTag 대체, GC 없음)
/// </summary>
public static class PlayerRegistry
{
    private static readonly List<PlayerRegistryMember> players = new();
    public static IReadOnlyList<PlayerRegistryMember> Players => players;

    public static void Register(PlayerRegistryMember p)
    {
        if (!players.Contains(p)) players.Add(p);
    }

    public static void Unregister(PlayerRegistryMember p) => players.Remove(p);

    /// <summary>
    /// 네트워크로 받은 PhotonView ID → 타겟 오브젝트 (ChaseTrigger가 쓰는 것과 같은 오브젝트)
    /// </summary>
    public static GameObject FindByViewId(int viewId)
    {
        foreach (PlayerRegistryMember p in players)
            if (p.View != null && p.View.ViewID == viewId) return p.gameObject;

        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => players.Clear();
}
