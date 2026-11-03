using System;
using System.Collections.Generic;
using static CommonStruct;

public class NormalMonster : Character, IDamageable, IAttacker
{
    public int Id;
    public float Speed;
    public float NowSpeed;
    public int AtkPoint;
    public int FinalAtkPoint;
    public int InstanceId;
    public string SpawnArea;
    public string Description;
    public DamageType DamageType { get; set; }

    public Action OnDead;

    public  NormalMonster(string name, int hp, Dictionary<DamageType, int> immune,float speed,
        int id, int instanceId, string spawnArea,string description, DamageType damageType
        ) : base(name, hp, immune) 
    {
        Id = id;
        InstanceId = instanceId;
        SpawnArea = spawnArea;
        Description = description;
        DamageType = damageType;
        Speed = speed;
        FinalAtkPoint = AtkPoint;
    }

    public void TakeDamage(AttackInfo attackInfo)
    {
        
    }

    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker)
    {
    }

    public void TakeDamage(AttackInfo attackInfo, Character attacker)
    {
    }
}
