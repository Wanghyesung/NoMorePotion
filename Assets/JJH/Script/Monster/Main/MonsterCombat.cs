using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CommonStruct;
using static JJH_Enums;
using SF = UnityEngine.SerializeField;
[RequireComponent(typeof(MonsterAnimationController))]

[RequireComponent(typeof(MonsterMove))]
public class MonsterCombat : MonoBehaviour, IDamageable
{
    [SF] protected MonsterAnimationController anim;
    [SF] protected MonsterMove mover;
    [SF] protected MonsterHPHud hud;
    [SF] private Collider biteHitbox;
    [SF] protected int prefabId;
    protected NormalMonster data;
    [ShowInInspector, ReadOnly]
    private int DebugHP => data != null ? data.HP : -1;
    [Button(ButtonSizes.Large), GUIColor(0, 1, 0)]
    public void DamageTest()
    {
        TakeDamage(new AttackInfo()
        {
            Damage = 100
        });
    }

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
    }

    // 스포너 구현 + 테스트 필요 없을 시 삭제
    protected virtual void InitTestData(int id, int hpOverride = -1)
    {
        MonsterData monsterData = EnemyDataLoader.Instance.LoadNormalData(id);
        MonsterTextData textData = EnemyDataLoader.Instance.LoadTextData(id);

        if (monsterData == null || textData == null) return;

        int hp = hpOverride > 0 ? hpOverride : monsterData.HP;

        Dictionary<DamageType, int> immuneVal = monsterData.Immune
                    .Where(kv => Enum.IsDefined(typeof(DamageType), kv.Key))
                    .ToDictionary(kv => (DamageType)kv.Key, kv => kv.Value);
        // 인스턴트 아이디는 차후 스폰 시 구분용
        Init(textData.Name, hp, hp, monsterData.AtkPoint, 
            monsterData.AttackPower, immuneVal, monsterData.Speed, monsterData.Id, 0,
            monsterData.SpawnArea,textData.Description, monsterData.DamageType);
    }

    protected virtual void OnDisable()
    {
        if(data != null)
            data.OnDead -= MonsterDead;
    }

    protected void SetData(NormalMonster newData)
    {
        if (data != null)
        {
            data.OnDead -= MonsterDead;
            if (hud != null) data.OnHPChanged -= hud.HealthChange;
        }

        data = newData;
        if (data == null) return;

        data.OnDead += MonsterDead;

        if (hud != null)
        {
            hud.Init(data.MaxHP, data.HP, data.Name);
            data.OnHPChanged += hud.HealthChange;
        }

        mover.StartAI(data.Speed, this).Forget();
    }

    public void Init(string name,int maxHp, int hp, int atkPoint, int attackPower, Dictionary<DamageType, int> immune,float speed,
        int id, int instanceId, string spawnArea, string description, DamageType damageType)
    {
        SetData(new(name, maxHp, hp, atkPoint, attackPower, immune, speed, id, instanceId, 
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
    public NormalMonster GetData()
    {
        return data;
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
        mover.state.ChangeState(mover.stateGroup[MonsterState.Death]);
        mover.StopAI();
    }
    #endregion

    #region 인터페이스
    public void TakeDamage(AttackInfo attackInfo)
    {
        ApplyDamage(attackInfo.Damage);
    }

    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker)
    {
        ApplyDamage(attackInfo.Damage);
    }

    public void TakeDamage(AttackInfo attackInfo, Character attacker)
    {
        ApplyDamage(attackInfo.Damage);
    }

    private void ApplyDamage(int damage)
    {
        if (data == null || data.HP <= 0) return;

        int before = data.HP;
        data.HP = Mathf.Max(0, data.HP - damage);
        data.OnHPChanged?.Invoke(before, data.HP);
        if (data.HP <= 0) data.OnDead?.Invoke();
    }

    #endregion
}
