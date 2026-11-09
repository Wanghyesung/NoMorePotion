using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class WaveData
{
    [SerializeField] private List<WaveEntry> entries;
    [SerializeField] private float spawnInterval;

    public IReadOnlyList<WaveEntry> Entries => entries;
    public float SpawnInterval => spawnInterval;
}
