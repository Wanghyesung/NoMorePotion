using System;
using System.Collections.Generic;
using UnityEngine;
using static JJH_Enums;

public class BossMonster : NormalMonster
{
    public int MinCounter;
    /// <summary>
    /// int 1 : 현재 카운터 누적 수치
    /// int 2 : 총 카운터 요구 수치
    /// </summary>
    public Action<int, int> OnCounterAccumed;
    public List<BossSkill> BossSkill;
    public BossMonster(string name, int maxHp, int hp, int atkPoint, int attackPower, 
        Dictionary<DamageType, int> immune, float speed, int id, int instanceId, 
        SpawnAreaType spawnArea, string description,
        List<BossSkill> bossSkill, DamageType damageType, int minCounter) 
        : base(name, maxHp, hp, atkPoint, attackPower, immune, speed, id, instanceId, 
            spawnArea, description, damageType)
    {
        BossSkill = bossSkill != null ? new(bossSkill) : new();
        MinCounter = minCounter;
    }
}
