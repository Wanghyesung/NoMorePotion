using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public abstract class Character
{
    public string Name;
    public int HP;

    /// <summary>
    /// <para>None = 0</para>
    /// <para>Normal = 1</para>
    /// <para>Fire = 2</para>
    /// <para>Ice = 3</para>
    /// <para>Electric = 4</para>
    /// </summary>
    public Dictionary<DamageType, int> Immune;

    public Character(string Name, int HP, Dictionary<DamageType, int> Immune)
    {
        this.Name = Name;
        this.HP = HP;
        this.Immune = Immune;
    }
    
}
