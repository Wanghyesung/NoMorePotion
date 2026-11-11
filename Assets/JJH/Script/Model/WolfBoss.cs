using System.Collections.Generic;
using UnityEngine;
using static JJH_Enums;

public class WolfBoss : BossMonster
{
    public int NormalWolfHP;

    public WolfBoss(string name, int maxHp, int hp,int atkPoint,int attackPower, Dictionary<DamageType, int> immune, 
        float speed, int id, int instanceId, SpawnAreaType spawnArea, string description, 
        List<BossSkill> bossSkill, int normalWolfHP, DamageType damageType) : 
        base(name, maxHp, hp, atkPoint, attackPower, immune, speed, id, instanceId, spawnArea, description, bossSkill, damageType)
    {
        NormalWolfHP = normalWolfHP;
    }
}
