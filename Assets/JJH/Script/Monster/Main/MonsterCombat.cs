using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CommonStruct;
using SF = UnityEngine.SerializeField;
[RequireComponent(typeof(MonsterAnimationController))]

[RequireComponent(typeof(MonsterMove))]
public class MonsterCombat : MonoBehaviour, IDamageable
{
    [SF] protected MonsterAnimationController anim;
    [SF] protected MonsterMove mover;
    [SF] private Collider biteHitbox;
    [SF] protected int prefabId;
    protected NormalMonster data;

    private void Awake()
    {
        if(anim == null)
            anim = GetComponent<MonsterAnimationController>();

        if(mover == null)
            mover = GetComponent<MonsterMove>();
    }

    private void Start()
    {
        InitTestData(prefabId);

        Debug.Log(data.ToString());
    }

    // 스포너 구현 + 테스트 필요 없을 시 삭제
    protected virtual void InitTestData(int id)
    {
        MonsterData data = EnemyDataLoader.Instance.LoadNormalData(id);
        MonsterTextData textData = EnemyDataLoader.Instance.LoadTextData(id);

        if (data == null || textData == null) return;

        Dictionary<DamageType, int> immuneVal = data.Immune
                    .Where(kv => Enum.IsDefined(typeof(DamageType), kv.Key))
                    .ToDictionary(kv => (DamageType)kv.Key, kv => kv.Value);
        // 인스턴트 아이디는 차후 스폰 시 구분용
        Init(textData.Name,data.HP,data.HP,
            immuneVal, data.Speed, data.Id, 0, 
            data.SpawnArea,textData.Description, data.DamageType);
    }

    protected virtual void OnDisable()
    {
        if(data != null)
            data.OnDead -= MonsterDead;
    }

    protected void SetData(NormalMonster newData)
    {
        if (data != null)
            data.OnDead -= MonsterDead;

        data = newData;

        if (data != null)
            data.OnDead += MonsterDead;

        mover.StartAI(data.Speed).Forget();
    }

    public void Init(string name,int maxHp, int hp, Dictionary<DamageType, int> immune,float speed,
        int id, int instanceId, string spawnArea, string description, DamageType damageType)
    {
        SetData(new(name, maxHp, hp, immune, speed, id, instanceId, 
            spawnArea, description, damageType));
    }

    #region
    public int GetDamage()
    {
        return data.AtkPoint;
    }

    public DamageType GetDamageType()
    {
        return data.DamageType;
    }
    #endregion

    #region 전투 애니메이션 용
    private void NormalAttackAnimationStart()
    {
        mover.IsAnimation = true;
    }

    private void NormalAttackOn()
    {
        biteHitbox.gameObject.SetActive(true);
    }

    private void NormalAttackOff()
    {
        biteHitbox.gameObject.SetActive(false);
    }

    private void NormalAttackAnimationEnd()
    {
        mover.IsAnimation = false;
        anim.IsNormalAttack(false);
    }

    private void MonsterDead()
    {
        anim.IsDead();
    }
    #endregion

    #region 인터페이스
    public void TakeDamage(AttackInfo attackInfo)
    {
        data.HP = Mathf.Max(0, data.HP - attackInfo.Damage);
    }

    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker)
    {
        data.HP = Mathf.Max(0, data.HP - attackInfo.Damage);
    }

    public void TakeDamage(AttackInfo attackInfo, Character attacker)
    {
        data.HP = Mathf.Max(0, data.HP - attackInfo.Damage);
    }
    #endregion
}
