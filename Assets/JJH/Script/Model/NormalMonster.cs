using System;
using System.Collections.Generic;
using static CommonStruct;
using static JJH_Enums;

public class NormalMonster : Character, IAttacker
{
    public int Id;
    public float Speed;
    public float NowSpeed;
    public int AtkPoint;
    public int AttackPower;
    public int FinalAtkPoint;
    public int InstanceId;
    public SpawnAreaType SpawnArea;
    public string Description;
    public DamageType DamageType { get; set; }

    public Action OnDead;
    /// <summary>
    ///  피격시 체력 반환 함수(int : 변경 전 체력, int : 변경 후 체력)
    /// </summary>
    public Action<int, int> OnHPChanged;

    public  NormalMonster(string name, int maxHp, int hp,int atkPoint, int attackPower, Dictionary<DamageType, int> immune,float speed,
        int id, int instanceId, SpawnAreaType spawnArea,string description, DamageType damageType
        ) : base(name, maxHp, hp, immune) 
    {
        Id = id;
        InstanceId = instanceId;
        SpawnArea = spawnArea;
        Description = description;
        DamageType = damageType;
        Speed = speed;
        NowSpeed = speed;
        AtkPoint = atkPoint;
        FinalAtkPoint = AtkPoint;
        AttackPower = attackPower;
    }

    public override string ToString()
    {
        return $"{Name}({Id}) : {HP} / {MaxHP}";
    }
}
