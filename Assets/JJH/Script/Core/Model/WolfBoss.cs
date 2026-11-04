using System.Collections.Generic;
using UnityEngine;

public class WolfBoss : NormalMonster
{
    public List<BossSkill> BossSkill;

    public WolfBoss(string name, int hp, Dictionary<DamageType, int> immune, 
        float speed, int id, int instanceId, string spawnArea, string description, 
        DamageType damageType) : 
        base(name, hp, immune, speed, id, instanceId, spawnArea, description, damageType)
    {
    }

   
}
