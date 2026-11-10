using Enzeun.Runtime;
using System.Collections.Generic;
using System;
using UnityEngine;

[CreateAssetMenu(fileName ="RegionSpawnTable", menuName ="Spawn/RegionWO")]
public class RegionSpawnTable : ScriptableObject
{
    [Serializable] public class Entry
    {
        public WorldObject prefab;
        public float weight;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    [SerializeField] private int keepCount;
    [SerializeField] private float respawnDelayMin;
    [SerializeField] private float respawnDelayMax;


    public int KeepCount => keepCount;


    // 가중치 랜덤
    public WorldObject PickRandom()
    {
        float total = 0f;
        foreach(Entry entry in entries)
        {
            if (IsValid(entry))
                total += entry.weight;
        }

        if (total <= 0f) return null;

        float r = UnityEngine.Random.Range(0f, total);
        WorldObject last = null;

        foreach(Entry entry in entries)
        {
            if (!IsValid(entry))
                continue;

            last = entry.prefab;
            r -= entry.weight;

            if (r < 0f)
                return entry.prefab;
        }

        return last;
    }

    public float GetRespawnDelay()
    {
        return UnityEngine.Random.Range(respawnDelayMin, respawnDelayMax);
    }

    private static bool IsValid(Entry entry)
    {
        return entry != null && entry.prefab != null && entry.weight > 0f;
    }
}
