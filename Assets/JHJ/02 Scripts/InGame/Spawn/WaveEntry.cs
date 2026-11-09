using System;
using UnityEngine;

[Serializable]
public class WaveEntry
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int count;

    public GameObject Prefab => prefab;
    public int Count => count;
}
