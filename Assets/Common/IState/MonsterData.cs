using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MonsterData
{
    public int Id;
    public int AtkPoint;
    public int AttackPower;
    public int HP;
    public float Speed;
    [JsonConverter(typeof(StringEnumConverter))]
    public DamageType DamageType;
    /// <summary>
    /// <para>None = 0</para>
    /// <para>Normal = 1</para>
    /// <para>Fire = 2</para>
    /// <para>Ice = 3</para>
    /// <para>Electric = 4</para>
    /// <para>Dark = 5</para>
    /// </summary>
    public Dictionary<int, int> Immune;
    public string SpawnArea;
}
