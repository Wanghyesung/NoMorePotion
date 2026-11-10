using Cysharp.Threading.Tasks;
using DamageNumbersPro;
using DG.Tweening;
using Enzeun.Runtime;
using Enzeun.SO;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;


using static CommonStruct;
using static JJH_Constants;
using static JJH_Enums;

using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(MonsterAnimationController))]
[RequireComponent(typeof(MonsterMove))]
[RequireComponent(typeof(BuffContainer))]
public class MonsterCombat : MonoBehaviour, IDamageable, IHealable
{
    [Serializable]
    public class SkillHitbox
    {
        public string Name;
        public Collider Hitbox;
    }

    [SF] protected MonsterAnimationController anim;
    [SF] protected MonsterMove mover;
    [SF] protected MonsterHPHud hud;
    [SF] protected BuffContainer statusEffect;
    [SF] private Collider biteHitbox;
    [SF] protected int prefabId;
    [SF] private List<SkillHitbox> skillHitboxes = new();

    [Header("사망 연출용")]
    [SF] private float sinkDuration = 2f;
    [SF] private float sinkMargin = 0.3f;

    [Header("데미지 숫자")]
    [SF] private DamageNumber damageNumberPrefab;
    [SF] private DamageNumber healNumberPrefab;

    private Renderer[] bodyRenderers;
    protected NormalMonster data;

    private CancellationTokenSource deadCts;
    private bool isSinking;

    [Button(ButtonSizes.Large), GUIColor(0, 1, 0)]
    public void DamageTest()
    {
        TakeDamage(new AttackInfo()
        {
            Damage = 100
        }, new SH(DamageType.Fire));
    }

    private class SH : IAttacker
    {
        public DamageType DamageType { get; set; }

        public SH(DamageType type)
        {
            DamageType = type;
        }
    }

    private void Awake()
    {
        if(anim == null)
            anim = GetComponent<MonsterAnimationController>();

        if(mover == null)
            mover = GetComponent<MonsterMove>();

        if(statusEffect == null)
            statusEffect = GetComponent<BuffContainer>();
        if (hud == null)
            hud = GetComponentInChildren<MonsterHPHud>();

        CacheRenderers();
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

    protected virtual void OnEnable()
    {
        if (statusEffect != null)
        {
            statusEffect.OnEffectAdded += ClipSpeedSet;
            statusEffect.OnEffectRemoved += ClipSpeedReset;
        }
            
    }

    protected virtual void OnDisable()
    {
        if(data != null)
            data.OnDead -= MonsterDead;

        if(statusEffect != null)
        {
            statusEffect.OnEffectAdded -= ClipSpeedSet;
            statusEffect.OnEffectRemoved -= ClipSpeedReset;
        }
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

    #region 스킬용
    public void SkillAnimationStart() => mover.IsAnimation = true;
    public void SkillHitOn(string skillName) => SetSkillHitbox(skillName, true);
    public void SkillHitOff(string skillName) => SetSkillHitbox(skillName, false);

    public void SkillAnimationEnd(string skillName)
    {
        SetSkillHitbox(skillName, false);
        mover.IsAnimation = false;
        anim.SetSkill(skillName, false);
    }

    private void SetSkillHitbox(string skillName, bool on)
    {
        foreach (SkillHitbox h in skillHitboxes)
        {
            if (h.Name != skillName) continue;
            h.Hitbox.gameObject.SetActive(on);
            return;
        }

        Debug.LogWarning($"[{name}] 스킬 히트박스 없음: {skillName}", this);
    }

    /// <summary>
    /// 보스 데이터일 때만 스킬 반환, 일반 몬스터는 null
    /// </summary>
    public BossSkill GetBossSkill(string animationName)
    {
        return (data as BossMonster)?.BossSkill?.Find(x => x.AnimationName == animationName);
    }
    #endregion

    #region 전투 처리 관련 함수
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

    public BuffContainer GetStatusEffect()
    {
        return statusEffect;
    }

    private void TryProvoke()
    {
        if (!NetAuthority.IsAuthority) return;      // 판단은 마스터에서만
        if (data == null || data.HP <= 0) return;   // 이번 공격으로 죽었으면 무시

        GameObject nearest = mover.FindNearestPlayer();
        if (nearest != null)
            mover.Provoke(nearest);
    }
    #endregion

    #region 전투 애니메이션 용
    public void NormalAttackAnimationStart()
    {
        mover.IsAnimation = true;
    }

    public void NormalAttackOn()
    {
        biteHitbox.gameObject.SetActive(true);
    }

    public void NormalAttackOff()
    {
        biteHitbox.gameObject.SetActive(false);
    }

    public void NormalAttackAnimationEnd()
    {
        mover.IsAnimation = false;
        anim.IsNormalAttack(false);
    }

    /// <summary>
    /// 사망 클립 끝 애니메이션 이벤트
    /// </summary>
    public void DeathAnimationEnd()
    {
        if (isSinking || data == null || data.HP > 0) return;   // 중복 실행, 오작동 방지
        StartDeathSink();
    }

    /// <summary>
    /// 속도 제어 반응 함수(속도가 감소했을 때)
    /// </summary>
    /// <param name="_"></param>
    private void ClipSpeedSet(ActiveStatusEffect _)
    {
        if (data == null) return;
        float speedMultiplier = statusEffect.GetStatMultiplier(StatType.MoveSpeed);
        data.NowSpeed = data.Speed * speedMultiplier;
        anim.SetMoveAnimationSpeed(speedMultiplier);
        mover.SetMoveSpeed(data.NowSpeed);
    }

    private void ClipSpeedReset(ActiveStatusEffect _)
    {
        if (data == null) return;

        anim.SetMoveAnimationSpeed();
        data.NowSpeed = data.Speed;
        mover.SetMoveSpeed(data.Speed);
    }
    #endregion

    #region 인터페이스
    public void TakeDamage(AttackInfo attackInfo)
    {
        HPChange(CalcFinalDamage(attackInfo.Damage, null));
        TryProvoke();
    }

    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker)
    {
        HPChange(CalcFinalDamage(attackInfo.Damage, attacker?.DamageType));
        
        TryProvoke();
    }

    public void TakeDamage(AttackInfo attackInfo, Character attacker)
    {
        HPChange(attackInfo.Damage);
        TryProvoke();
    }

    public void Heal(int amount)
    {
        HPChange(-amount);
    }

    public float MultiplierCalc(float val, float multiplier)
    {
        return Mathf.Max(0, val * multiplier);
    }

    private void HPChange(int damage)
    {
        if (data == null || data.HP <= 0) return;

        int before = data.HP;
        data.HP = Mathf.Max(0, data.HP - damage);
        data.OnHPChanged?.Invoke(before, data.HP);
        ShowNumber(damage);
        if (data.HP <= 0) data.OnDead?.Invoke();
    }

    /// <summary>
    /// 기본 내성 + 내성 상태 변화를 적용한 최종 피해량
    /// </summary>
    /// <param name="damage"></param>
    /// <param name="type"></param>
    /// <returns>최종 피해량 int</returns>
    private int CalcFinalDamage(int damage, DamageType? type)
    {
        if (data == null) return 0;

        float rate = 100f;

        if (type.HasValue && data.Immune.TryGetValue(type.Value, out int immune))
            rate = Mathf.Max(0f, rate - immune);

        if (statusEffect != null)
            rate /= Mathf.Max(0.01f, statusEffect.GetStatMultiplier(StatType.Defense));

        return Mathf.Max(0, Mathf.RoundToInt(damage * rate / 100f));
    }

    #endregion

    #region 사망 정보용
    

    private void CacheRenderers()
    {
        // 몸체 메시만 (파티클 이펙트 등은 제외)
        bodyRenderers = GetComponentsInChildren<Renderer>()
            .Where(r => r is SkinnedMeshRenderer || r is MeshRenderer)
            .ToArray();
    }

    private float GetBodyHeight()
    {
        if (bodyRenderers.Length == 0) return 1f;

        Bounds b = bodyRenderers[0].bounds;
        for (int i = 1; i < bodyRenderers.Length; i++)
            b.Encapsulate(bodyRenderers[i].bounds);

        return b.max.y - transform.position.y;
    }

    private void MonsterDead()
    {
        anim.IsDead();
        mover.state.ChangeState(mover.stateGroup[MonsterState.Death]);
        mover.StopAI();
    }

    private void StartDeathSink()
    {
        CancelDeathSink();
        isSinking = true;
        deadCts = new();
        MonsterDeadAnimation(deadCts.Token).Forget();
    }

    private async UniTaskVoid MonsterDeadAnimation(CancellationToken token)
    {
        mover.agent.enabled = false;   // 켜두면 NavMesh 높이로 다시 끌어올림
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        float depth = GetBodyHeight() + sinkMargin;

        bool canceled = await transform.DOMoveY(-depth, sinkDuration)
            .SetRelative()
            .SetEase(Ease.InQuad)
            .ToUniTask(TweenCancelBehaviour.Kill, token)
            .SuppressCancellationThrow();
        if (canceled) return;

        // 풀링시 변경
        gameObject.SetActive(false);
    }

    private void CancelDeathSink()
    {
        isSinking = false;
        deadCts?.Cancel();
        deadCts?.Dispose();
        deadCts = null;
    }
    #endregion

    #region 기타
    /// <summary>
    /// 피격 시 대미지 효과 출력 함수
    /// </summary>
    /// <param name="value"></param>
    private void ShowNumber(int value)
    {
        if (value == 0) return;

        // 머리 위: 사망 연출에서 만든 몸 높이 계산을 재사용
        Vector3 pos = transform.position + Vector3.up * GetBodyHeight();

        if (value > 0) damageNumberPrefab.Spawn(pos, value, transform);
        else healNumberPrefab.Spawn(pos, -value, transform);
    }
    #endregion
}
