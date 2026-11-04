using System.Collections.Generic;
using UnityEngine;

public class WolfBoss : NormalMonster
{
    public List<BossSkill> BossSkill;

    public WolfBoss(string name, int maxHp, int hp, Dictionary<DamageType, int> immune, 
        float speed, int id, int instanceId, string spawnArea, string description, 
        List<BossSkill> bossSkill, DamageType damageType) : 
        base(name, maxHp, hp, immune, speed, id, instanceId, spawnArea, description, damageType)
    {
        BossSkill = new(bossSkill);
    }

   
}
