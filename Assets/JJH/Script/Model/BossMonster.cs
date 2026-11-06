using System.Collections.Generic;
using UnityEngine;

public class BossMonster : NormalMonster
{
    public List<BossSkill> BossSkill;

    public BossMonster(string name, int maxHp, int hp, int atkPoint, int attackPower, 
        Dictionary<DamageType, int> immune, float speed, int id, int instanceId, 
        string spawnArea, string description,
        List<BossSkill> bossSkill, DamageType damageType) 
        : base(name, maxHp, hp, atkPoint, attackPower, immune, speed, id, instanceId, 
            spawnArea, description, damageType)
    {
        BossSkill = bossSkill != null ? new(bossSkill) : new();
    }
}
