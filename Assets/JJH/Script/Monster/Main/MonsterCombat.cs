using Cysharp.Threading.Tasks;
using DamageNumbersPro;
using DG.Tweening;
using Enzeun.Runtime;
using Enzeun.SO;
using Photon.Pun;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MoreMountains.Feedbacks;
using UnityEngine;


using static CommonStruct;
using static JJH_Constants;
using static JJH_Enums;

using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(MonsterAnimationController))]
[RequireComponent(typeof(MonsterMove))]
[RequireComponent(typeof(BuffContainer))]
public class MonsterCombat : MonoBehaviourPun, IDamageable, IHealable, IPoolable
{
    [SF] protected MonsterAnimationController anim;
    [SF] protected MonsterMove mover;
    [SF] protected MonsterHPHud hud;
    [SF] protected MonsterVfx vfx;
    [SF] protected AttackAim aim;
    [SF] private UseProjectile projectileShooter;
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

    [Header("카운터")]
    // 카운터 서버 지연 보정 길이
    [SF] private float counterGrace = 0.2f;
    // CounterMat
    [SF] private Material counterOverlay;
    [SF] private GameObject counterEffect;
    [SF] private MMF_Player counterFeedback;

    private bool counterOpen;
    private float counterClosedAt = -999f;
    private bool skillCanceled;

    private Renderer[] bodyRenderers;
    protected NormalMonster data;

    private CancellationTokenSource deadCts;
    private bool isSinking;
    private bool fromPool;

    public event Action<MonsterCombat> Died;
    public event Action<MonsterCombat> Damaged;

    public bool IsSkillCanceled => skillCanceled;

    private Material[][] originalMaterials;
    private GameObject counterEffectObj;

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
        if (vfx == null && TryGetComponent(out MonsterVfx vfxVal))
            vfx = vfxVal;
        if (aim == null && TryGetComponent(out AttackAim aimVal))
            aim = aimVal;
        if (projectileShooter == null && TryGetComponent(out UseProjectile projectileVal))
            projectileShooter = projectileVal;
        
        CacheRenderers();
    }

    private void Start()
    {
        if (!fromPool) InitData(prefabId);
    }

    // 스포너 구현 + 테스트 필요 없을 시 삭제
    protected virtual void InitData(int id, int hpOverride = -1)
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
        SetCounterOverlay(false);

        if (counterEffectObj != null)
            PoolManager.Instance.Release(counterEffectObj);
        if (counterFeedback != null)
            counterFeedback.StopFeedbacks();
        counterEffectObj = null;

        CancelDeathSink();
        if (data != null)
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
        int id, int instanceId, SpawnAreaType spawnArea, string description, DamageType damageType)
    {
        SetData(new(name, maxHp, hp, atkPoint, attackPower, immune, speed, id, instanceId, 
            spawnArea, description, damageType));
    }

    #region 스킬용

    public void CounterOn()
    {
        if (skillCanceled) return;
        counterOpen = true;
        SetCounterOverlay(true);
    }

    public void CounterOff()
    {
        counterOpen = false;
        counterClosedAt = Time.time;
        SetCounterOverlay(false);
    }

    public void SkillAnimationStart()
    {
        skillCanceled = false;    // 새 스킬마다 초기화
        mover.IsAnimation = true;
        if (vfx != null) vfx.ResetWarningPositions();
        if (aim != null) aim.Aim();
    }
    public void SkillHitOn(int skillNum, float skillDist, float duration)
    {
        if (skillCanceled) return;   // 끊긴 스킬의 판정은 무시)
        if (skillDist > 0f && GameAuthority.HasAuthority)
            mover.SetManualMove(skillDist, duration);
        
        SetSkillHitbox(skillNum, true);
    }
    public void SkillHitOff(int skillNum, float skillDist)
    {
        if (skillDist > 0f && GameAuthority.HasAuthority)
            mover.EndManualMove();

        SetSkillHitbox(skillNum, false);
    }

    public void SkillAnimationEnd(int skillNum)
    {
        mover.EndManualMove();

        SetSkillHitbox(skillNum, false);
        SetCounterOverlay(false);

        mover.IsAnimation = false;
        anim.SetSkill(skillNum, false);
        if (aim != null) aim.ResetAim();
    }

    private void SetSkillHitbox(int skillNum, bool on)
    {
        foreach (SkillHitbox hitbox in skillHitboxes)
        {
            if (hitbox.SkillNum != skillNum) continue;
            if (on && hitbox.FollowWarningSlot >= 0 && vfx != null)
                hitbox.Hitbox.transform.position =
                    vfx.GetLastWarningPosition(hitbox.FollowWarningSlot);

            hitbox.Hitbox.gameObject.SetActive(on);
            return;
        }

        if (on)
            Debug.LogWarning($"[{name}] 스킬 히트박스 없음: {skillNum}", this);
    }



    /// <summary>
    /// 자신 화면 기준으로 카운터를 성공했을 경우 호출
    /// </summary>
    public void RequestCounter()
    {
        if (!counterOpen) return;

        if (PhotonNetwork.InRoom && !GameAuthority.HasAuthority)
            photonView.RPC(nameof(RpcRequestCounter), RpcTarget.MasterClient);
        else
            RpcRequestCounter();
    }

    /// <summary>
    /// 보스 데이터일 때만 스킬 반환, 일반 몬스터는 null
    /// </summary>
    public BossSkill GetBossSkill(int skillNum)
    {
        return (data as BossMonster)?.BossSkill?.Find(x => x.SkillNum == skillNum);
    }
    #endregion

    #region 전투 처리 관련 함수
    public int GetDamage()
    {
        return data.AtkPoint;
    }

    /// <summary>
    /// 버프(공격력 배율)까지 반영한 공격력
    /// </summary>
    public float GetBuffedDamage()
    {
        float atkRate = statusEffect != null
            ? statusEffect.GetStatMultiplier(StatType.AttackPower) : 1f;
        return MultiplierCalc(GetDamage(), atkRate);
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
        if (!GameAuthority.HasAuthority) return;      // 판단은 마스터에서만
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
        if (aim != null) aim.Aim();
    }

    public void NormalAttackOn()
    {
        if(biteHitbox != null)
            biteHitbox.gameObject.SetActive(true);
        if (projectileShooter != null)
            projectileShooter.Fire();
        if (vfx != null)
            vfx.PlayHit(0);
    }

    public void NormalAttackOff()
    {
        if (biteHitbox != null)
            biteHitbox.gameObject.SetActive(false);
    }

    public void NormalAttackAnimationEnd()
    {
        mover.IsAnimation = false;
        anim.IsNormalAttack(false);
        if (aim != null) aim.ResetAim();
    }

    /// <summary>
    /// 사망 클립 끝 애니메이션 이벤트
    /// </summary>
    public void DeathAnimationEnd()
    {
        if (!GameAuthority.HasAuthority) return;
        if (isSinking || data == null || data.HP > 0) return;
        StartDeathSink();
    }

    public void ContinueDeathAnimation()
    {
        if (isSinking) return;
        StartDeathSink();
    }

    #region 카운터 연출
    private void StartCounterReaction()
    {
        counterEffectObj = 
            PoolManager.Instance
            .Get(counterEffect, transform.position, Quaternion.identity);

        anim.IsCountered();
        mover.IsAnimation = true;
        mover.agent.isStopped = true;
    }

    /// <summary>
    /// 몸체 렌더러에 카운터 머티리얼을 한 칸 덧씌우거나 원래대로 복구
    /// </summary>
    private void SetCounterOverlay(bool on)
    {
        if (counterOverlay == null || originalMaterials == null) return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Material[] src = originalMaterials[i];
            if (!on)
            {
                bodyRenderers[i].sharedMaterials = src;
                continue;
            }

            var mats = new Material[src.Length + 1];
            src.CopyTo(mats, 0);
            mats[^1] = counterOverlay;
            bodyRenderers[i].sharedMaterials = mats;
        }
    }
    public void CounterSuccessEnd()
    {
        anim.IsCountered(false);
        mover.IsAnimation = false;
        if (counterEffectObj != null)
            PoolManager.Instance.Release(counterEffectObj);
        counterEffectObj = null;
        mover.agent.isStopped = false;
    }
    #endregion

    /// <summary>
    /// 속도 제어 반응 함수(속도가 감소했을 때)
    /// </summary>
    /// <param name="_"></param>
    private void ClipSpeedSet(ActiveStatusEffect _) => ApplySpeedMultiplier();
    private void ClipSpeedReset(ActiveStatusEffect _) => ApplySpeedMultiplier();

    /// <summary>
    /// 현재 걸려 있는 효과 기준으로 이동속도 배율 재계산, 추가/제거 공용
    /// </summary>
    private void ApplySpeedMultiplier()
    {
        if (data == null) return;

        float speedMultiplier = statusEffect.GetStatMultiplier(StatType.MoveSpeed);
        data.NowSpeed = data.Speed * speedMultiplier;
        anim.SetMoveAnimationSpeed(speedMultiplier);
        mover.SetMoveSpeed(data.NowSpeed);
    }
    #endregion

    #region 인터페이스
    public void TakeDamage(AttackInfo attackInfo)
    {
        SendDamage(attackInfo.Damage, -1);
    }

    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker)
    {
        int type = attacker != null ? (int)attacker.DamageType : -1;

        SendDamage(attackInfo.Damage, type);
    }

    public void TakeDamage(AttackInfo attackInfo, Character attacker)
    {
        SendDamage(attackInfo.Damage, -1);
    }

    /// <summary>
    /// 기본 공격 데미지 (근접 히트박스·원거리 투사체 공용)
    /// </summary>
    public AttackInfo CreateNormalAttackInfo()
    {
        return new AttackInfo
        {
            Damage = Mathf.RoundToInt(GetBuffedDamage()),
            Position = transform.position
        };
    }

    /// <summary>
    /// 스킬 데미지 (버프 반영 공격력 × 스킬 배율)
    /// </summary>
    public AttackInfo CreateSkillAttackInfo(int skillNum)
    {
        BossSkill skill = GetBossSkill(skillNum);
        if (skill == null) return new AttackInfo();

        return new AttackInfo
        {
            Damage = Mathf.RoundToInt(GetBuffedDamage() * skill.SkillDamage / 100f),
            Position = transform.position
        };
    }

    /// <summary>
    /// 방장에게 피해 전달 (한 번의 타격만 카운터로 인정, 지속 피해 틱은 제외)
    /// </summary>
    private void SendDamage(int damage, int type)
    {
        bool counterHit = !HealthChangeModule.IsApplyingPeriodic;

        if (!GameAuthority.HasAuthority)
        {
            photonView.RPC(nameof(RpcTakeDamage), RpcTarget.MasterClient, 
                damage, type, counterHit);
            return;
        }

        RpcTakeDamage(damage, type, counterHit);
    }

    public void Heal(int amount)
    {
        if (!GameAuthority.HasAuthority)
        {
            photonView.RPC(nameof(RpcHeal), RpcTarget.MasterClient, amount);
            return;
        }
        RpcHeal(amount);
    }

    public float MultiplierCalc(float val, float multiplier)
    {
        return Mathf.Max(0, val * multiplier);
    }

    private void HPChange(int damage)
    {
        if (data == null || data.HP <= 0) 
            return;

        int before = data.HP;
        data.HP = Mathf.Clamp(data.HP - damage, 0, data.MaxHP);

        if (before == data.HP) 
            return;

        data.OnHPChanged?.Invoke(before, data.HP);
        if (before > data.HP) 
            Damaged?.Invoke(this);

        ShowNumber(before - data.HP);

        if (PhotonNetwork.InRoom)
            photonView.RPC(nameof(RpcSyncHP), RpcTarget.Others, data.HP);

        if (data.HP <= 0) 
            data.OnDead?.Invoke();
    }

    /// <summary>
    /// 기본 내성 + 내성 상태 변화를 적용한 최종 피해량
    /// </summary>
    /// <param name="damage"></param>
    /// <param name="type"></param>
    /// <returns>최종 피해량 int</returns>
    private int CalcFinalDamage(int damage, DamageType? type)
    {
        if (data == null) 
            return 0;

        if (type == DamageType.None) 
            return Mathf.Max(0, damage);

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
        originalMaterials = bodyRenderers.Select(r => r.sharedMaterials).ToArray();
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
        ResetState();

        SetCounterOverlay(false);
        anim.IsDead();
        mover.state.ChangeState(mover.stateGroup[MonsterState.Death]);

        if(TryGetComponent(out MonsterVfx vfx))
            vfx.HideAllWarnings();
        
        mover.StopAI();
        Died?.Invoke(this);

        // 보스 데이터면 게임 전체에 알림
        if (data is BossMonster boss)
            BossEvents.Defeat(boss);
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

        //늑대의 경우 WolfPack으로 한 무리를 지어 움직이기 때문에 따로 처리
        if (mover is WolfMove wolf && wolf.Pack != null)
        {
            gameObject.SetActive(false);
            wolf.Pack.OnMemberGone();
        }            
        else
            NetPool.Despawn(gameObject);
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

        Vector3 pos = transform.position + Vector3.up * GetBodyHeight();

        if (value > 0 && damageNumberPrefab != null) damageNumberPrefab.Spawn(pos, value, transform);
        else if (value < 0 && healNumberPrefab != null) healNumberPrefab.Spawn(pos, -value, transform);
    }

    /// <summary>
    /// 풀에서 꺼낼 때
    /// </summary>
    public void OnGetFromPool()
    {
        fromPool = true;
        CancelDeathSink();

        // 사망 연출에서 끈 것 복구 (꺼진 히트박스까지 찾도록 true)
        mover.agent.enabled = true;
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            c.enabled = true;

        // 히트박스는 꺼진 상태로 시작
        if (biteHitbox != null) biteHitbox.gameObject.SetActive(false);
        foreach (SkillHitbox h in skillHitboxes)
            h.Hitbox.gameObject.SetActive(false);
        if (aim != null) aim.ResetAim();
        mover.SetDefaultTarget(null);

        InitData(prefabId);
    }

    /// <summary>
    /// 풀에 반납할 때
    /// </summary>
    public void OnReleaseToPool()
    {
        ResetState();

        Died = null;
        CancelDeathSink();
        SetCounterOverlay(false);
        mover.StopAI();
    }
    #endregion

    #region RPC

    [PunRPC]
    protected void RpcTakeDamage(int damage, int typeNum, bool counterHit)
    {
        DamageType? type = typeNum >= 0 ? (DamageType)typeNum : null;
        HPChange(CalcFinalDamage(damage, type));

        if (counterHit)
            RpcRequestCounter();   // 카운터 창이 아니면 내부에서 무시

        TryProvoke();
    }

    [PunRPC]
    protected void RpcSyncHP(int hp)
    {
        if (data == null) return;
        int before = data.HP;
        data.HP = hp;
        data.OnHPChanged?.Invoke(before, hp);   // HUD 갱신
        ShowNumber(before - hp);
    }

    [PunRPC]
    protected void RpcHeal(int amount)
    {
        HPChange(-amount);
    }

    [PunRPC]
    protected void RpcRequestCounter()
    {
        // 마스터의 카운터 성공 판정 
        bool inWindow = counterOpen || Time.time - counterClosedAt <= counterGrace;
        if (!inWindow || skillCanceled) return;

        anim.StopActiveSkill();

        if (PhotonNetwork.InRoom)
            photonView.RPC(nameof(RpcSkillCanceled), RpcTarget.All);
        else
            RpcSkillCanceled();
    }

    [PunRPC]
    protected void RpcSkillCanceled()
    {
        skillCanceled = true;
        counterOpen = false;
        SetCounterOverlay(false);
        mover.EndManualMove();

        foreach (SkillHitbox h in skillHitboxes)
            h.Hitbox.gameObject.SetActive(false);

        if(vfx != null) 
            vfx.HideAllWarnings();
        if (aim != null) aim.ResetAim();

        anim.IsCountered();
        StartCounterReaction();

        if (counterFeedback != null)
            counterFeedback.PlayFeedbacks(transform.position);

    }
    #endregion

    #region 기타
    public void ResetState()
    {
        if (counterEffectObj != null)
            PoolManager.Instance.Release(counterEffectObj);

        if (counterFeedback != null)
            counterFeedback.StopFeedbacks();

        counterEffectObj = null;

        mover.EndManualMove();
    }
    #endregion
}
