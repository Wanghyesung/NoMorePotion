using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using static JJH_Constants;
using static JJH_Enums;

using HIn = UnityEngine.HideInInspector;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class MonsterMove : MonoBehaviourPunCallbacks
{

    #region 직렬화 데이터

    [Header("스크립트")]
    public MonsterAnimationController anim;
    public MonsterCombat combat;
    [SF] protected AttackSelector attackSelector = new();
    public AttackSelector AttackSelector => attackSelector;

    [Header("AI 네비게이션")]
    public NavMeshAgent agent;

    [Header("본체 관련")]
    public float nextMoveDist;
    [SF] private float attackRange = 1f;
    public SphereCollider chaseCollider;
    public float attackSpeed = 2;
    public float patrolDelay = 4;
    public float walkSpeed = 1.9f;
    public float runSpeed = 3.8f;
    public float turnSpeed = 5;
    public float NowSpeed => agent.speed;

    [Header("도달 불가 판정")]
    [SF] private ReachabilityTracker reachability = new();

    [Header("인스펙터")]
    [NonSerialized]
    public GameObject nowTarget;

    [Header("피격 어그로")]
    [SF] private float provokeDuration = 5f;
    [SF] private float provokeSearchRange = 50f;
    private float provokedUntil;

    [Header("공격 슬롯")]
    // 보스, 늑대는 프리팹에서 끄기
    [SF] private bool useAttackSlot = true;
    // 대기 거리
    [SF] private float waitBuffer = 2f;
    // 사거리 대비 정지 위치(기본값 0.7(70%)) 비율%
    [SF] private float slotStandRatio = 0.7f;
    // 도착 판정 거리
    [SF] private float slotTolerance = 1f;

    [Header("기본 타겟 (웨이브)")]
    [SF] private GameObject defaultTarget;

    #endregion

    #region 필드 변수

    [HIn] public StateMachine state;
    [HIn] public float patrolTimer = 0;
    [HIn] public bool patrolTimerActive;
    [HIn] public float attackTimer = 0;
    [HIn]public float AttackRange => attackRange;
    [HIn] public bool IsAnimation;
    private CancellationTokenSource aiCts;
    private AttackTarget targetInfo;
    private MonsterPatrolState patrolState;

    public Dictionary<MonsterState, IMonsterState> stateGroup; 
    public bool IsProvoked => Time.time < provokedUntil;
    public float ProvokeDuration => provokeDuration;
    public bool IsTargetObjective => targetInfo != null && targetInfo.IsObjective;
    private bool HasSlot => useAttackSlot && targetInfo != null && targetInfo.Holds(this);
    #endregion


    private void Awake()
    {
        if(agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.Warp(hit.position);
        }
        if(anim == null)
            anim = GetComponent<MonsterAnimationController>();
        if (combat == null)
            combat = GetComponent<MonsterCombat>();

        state = new();

        stateGroup = new Dictionary<MonsterState, IMonsterState>()
        {
            [MonsterState.Spawn] = new MonsterSpawnState(this),
            [MonsterState.Idle] = new MonsterIdleState(this),
            [MonsterState.Patrol] = new MonsterPatrolState(this),
            [MonsterState.Chase] = new MonsterChaseState(this),
            [MonsterState.Wait] = new MonsterWaitState(this),
            [MonsterState.Attack] = new MonsterAttackState(this),
            [MonsterState.Death] = new MonsterDeathState(this)
        };

        patrolState = (MonsterPatrolState)stateGroup[MonsterState.Patrol];
    }

    private void Start()
    {
        RegisterStates();
    }

    private void Update()
    {
        if (!IsAnimation && nowTarget != null && ShouldFaceTarget())
            AttackAngleRotate();
    }

    public override void OnDisable()
    {
        base.OnDisable();
        StopAI();
    }

    /// <summary>
    /// 몬스터의 타겟 변경 함수
    /// </summary>
    /// <param name="target">타겟 Object</param>
    public void SetTarget(GameObject target)
    {
        if (ReferenceEquals(nowTarget, target)) return;

        ReleaseSlot();          // 이전 대상의 슬롯 반납
        nowTarget = target;
        targetInfo = null;      // 이전 대상 정보 초기화

        if (target != null && target.TryGetComponent(out AttackTarget aTarget))
            targetInfo = aTarget;
    }


    /// <summary>
    /// 피격 시 가장 가까운 대상을 거리 제한 없이 일정 시간 추적
    /// </summary>
    /// <param name="target">가장 가까운 플레이어</param>
    public virtual void Provoke(GameObject target)
    {
        if (target == null || !CanAcquireTarget(target)) return;

        SetTarget(target);
        // 맞을 때마다 연장
        provokedUntil = Time.time + provokeDuration;   
    }

    /// <summary>
    /// 검색 범위 안에서 가장 가까운 추적 가능 플레이어
    /// </summary>
    public GameObject FindNearestPlayer()
    {
        GameObject best = null;
        float bestSqr = provokeSearchRange * provokeSearchRange;

        foreach (PlayerRegistryMember player in PlayerRegistry.Players)
        {
            GameObject playerObject = player.gameObject;
            if (!playerObject.activeInHierarchy || !CanAcquireTarget(playerObject)) continue;

            float sqr = (playerObject.transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = playerObject;
            }
        }
        return best;
    }

    /// <summary>
    /// 자식 클래스가 전용 상태(FSM)를 추가할 때 override
    /// </summary>
    protected virtual void RegisterStates() 
    { 

    }

    /// <summary>
    /// 타겟의 재추적 가능 여부 확인 오버라이드 가능 함수
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public virtual bool CanAcquireTarget(GameObject target) =>
        AttackTarget.IsValid(target) && reachability.CanAcquire(target);

    /// <summary>
    /// 개체의 회전 조절 여부
    /// </summary>
    /// <returns></returns>
    protected virtual bool ShouldFaceTarget()
    {
        if (state == null) return false;

        MonsterState s = state.GetStateType();
        return s == MonsterState.Attack || (s == MonsterState.Wait && agent.isStopped);
    }

    /// <summary>
    /// <para>대상 방향으로 몸체를 회전 시키는 함수</para>
    /// </summary>
    private void AttackAngleRotate()
    {
        float angleDeadzone = 1.5f;

        Vector3 targetDirection =
            nowTarget.transform.position - transform.position;
        targetDirection.y = 0;
        Quaternion targetRotation =
            Quaternion.LookRotation(targetDirection);

        float angleDifference = Vector3.Angle(transform.forward, targetDirection);

        if (angleDifference > angleDeadzone)
            transform.rotation =
            Quaternion.Slerp(transform.rotation,
            targetRotation, Time.deltaTime * turnSpeed);
        else
        {
            transform.rotation = targetRotation;
        }
    }

    /// <summary>
    /// 몬스터 생성/재사용 시 구동해야하는 함수
    /// </summary>
    /// <param name="speed"></param>
    /// <returns></returns>
    public async UniTask StartAI(float speed, MonsterCombat combat)
    {
        if (!agent.enabled) agent.enabled = true;
        attackSelector.ResetCooldowns();
        StopAI();
        anim.AnimatorRebind();
        reachability.ResetState();
        aiCts = new CancellationTokenSource();
        CancellationToken token = aiCts.Token;
        this.combat = combat;
        // 이전 풀 정보 초기화
        state = new();
        SetTarget(null);
        IsAnimation = false;
        patrolTimer = attackTimer = 0;
        patrolTimerActive = false;
        provokedUntil = 0;

        SetMoveSpeed(speed);
        agent.speed = walkSpeed;

        StartAILoop(token).Forget();
        await UniTask.Delay(FSM_TIMER, cancellationToken: token);

        if (state.GetStateType() == MonsterState.None ||
            state.GetStateType() == MonsterState.Spawn)
            state.ChangeState(stateGroup[MonsterState.Patrol]);
    }

    protected virtual IMonsterState UpdateState()
    {
        IMonsterState result = null;
        MonsterState nowState = MonsterState.None;

        //스폰 중엔 판단 정지
        if (state.GetStateType() == MonsterState.Spawn) return null;

        // 타겟이 죽었거나, 꺼졌거나, 파괴됐으면 타겟 종료
        if (!ReferenceEquals(nowTarget, null) && !AttackTarget.IsValid(nowTarget))
        {
            provokedUntil = 0;
            SetTarget(null);
        }

        // 도달 불가 상태가 유예 시간을 넘기면 추적 해제, 도발 추적 불가시 도발도 종료 
        if (nowTarget != null &&
            reachability.ShouldDrop(transform.position, nowTarget, FSM_TIMER / MIL_SEC_F))
        {
            provokedUntil = 0;
            SetTarget(null);
        }

        // 기본 타겟이 있는 경우 기본 타겟으로 재설정
        if (nowTarget == null && defaultTarget != null && defaultTarget.activeInHierarchy)
            SetTarget(defaultTarget);

        if (nowTarget == null)
        {
            MonsterState current = state.GetStateType();

            if (current == MonsterState.Patrol)
                nowState = patrolState.IsArrived ? MonsterState.Idle : MonsterState.Patrol;   // 도착하면 쉬기
            else if (current == MonsterState.Idle)
                nowState = patrolTimer >= patrolDelay ? MonsterState.Patrol : MonsterState.Idle;   // 다 쉬었으면 순찰
            else
                nowState = MonsterState.Patrol;   // 추적·공격 등에서 타겟을 잃으면 바로 순찰

            return current == nowState ? null : stateGroup[nowState];
        }
        
        float distance = DistanceToTarget();
        // 추적 가능한 상태인지 확인
        bool chaseable = distance <= chaseCollider.radius || IsProvoked || IsTargetObjective;
        bool slotZone = useAttackSlot && targetInfo != null && distance <= AttackRange + waitBuffer;

        // 공격 범위 안
        if (slotZone)
        {
            // 슬롯이 있을 경우
            if (TryHoldSlot())
            {
                if (IsInAttackRange(distance) && IsAtSlot())
                {
                    result = stateGroup[MonsterState.Attack];
                    nowState = MonsterState.Attack;
                }
                else
                {
                    result = stateGroup[MonsterState.Chase];
                    nowState = MonsterState.Chase;
                }
            }
            // 없을 경우
            else
            {
                result = stateGroup[MonsterState.Wait];
                nowState = MonsterState.Wait;
            }
        }
        else if (IsInAttackRange(distance))
        {
            // 슬롯 미사용 몬스터 (보스, 늑대 등)
            result = stateGroup[MonsterState.Attack];
            nowState = MonsterState.Attack;
        }
        // 공격 범위 밖이지만 감지 범위 안
        else if (chaseable)
        {
            result = stateGroup[MonsterState.Chase];
            nowState = MonsterState.Chase;
        }
        // 모든 조건 불충족 시
        else
        {
            result = stateGroup[MonsterState.Patrol];
            nowState = MonsterState.Patrol;
        }
        
        if (state.GetStateType() == nowState)
            result = null;

        return result;
    }

    private async UniTaskVoid StartAILoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!IsAnimation)
            {
                IMonsterState data = UpdateState();
                
                if (data != null)
                    state.ChangeState(data);

                if (patrolTimerActive)
                    patrolTimer += FSM_TIMER / MIL_SEC_F;

                attackTimer = Mathf.Min(attackTimer + FSM_TIMER / MIL_SEC_F, attackSpeed);
                
                state.Stay();
            }

            
            await UniTask.Delay(FSM_TIMER, cancellationToken: token);
        }
    }

    public void SetMoveSpeed(float speed)
    {
        runSpeed = speed;
        walkSpeed = speed / 2;
    }

    #region 공격 대기 슬롯 관련

    /// <summary>
    /// 슬롯이 있으면 배정된 자리, 없으면 대상 위치를 반환하는 함수
    /// </summary>
    /// <returns>배정 위치(Vector3)</returns>
    public Vector3 GetChaseDestination()
    {
        if (HasSlot) return targetInfo.GetSlotPoint(this, attackRange * slotStandRatio);
        return nowTarget.transform.position;
    }

    
    /// <summary>
    /// 대상 둘레의 바깥 고리의 위치를 반환하는 함수 (지금 있는 방향은 유지)
    /// </summary>
    /// <returns>둘레 바깥 고리의 위치(Vector3)</returns>
    public Vector3 GetWaitPoint()
    {
        Vector3 center = nowTarget.transform.position;
        Vector3 dir = transform.position - center;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;

        float radius = (targetInfo != null ? targetInfo.BodyRadius : 0f) + attackRange + waitBuffer;
        return center + dir.normalized * radius;
    }


    public bool IsInAttackRange(float distance) =>
    distance <= attackRange || attackSelector.AnyReadyInRange(distance);
    
    /// <summary>
    /// 기본 대상 지정 함수
    /// <para>기본 대상이 있을 경우 목표를 잃으면 기본 대상을 추적함</para>
    /// </summary>
    /// <param name="target"></param>
    public void SetDefaultTarget(GameObject target) => defaultTarget = target;

    /// <summary>
    /// 대상의 몸 반지름을 뺀 실제 거리 (큰 대상도 표면 기준으로 판정)
    /// </summary>
    public float DistanceToTarget()
    {
        if (nowTarget == null) return float.MaxValue;

        float dist = Vector3.Distance(transform.position, nowTarget.transform.position);
        return targetInfo != null ? Mathf.Max(0f, dist - targetInfo.BodyRadius) : dist;
    }
    /// <summary>
    /// 슬롯 반납 함수
    /// </summary>
    public void ReleaseSlot()
    {
        if (targetInfo != null) targetInfo.Release(this);
    }

    // 슬롯 미사용 몬스터 or 슬롯 정보 없는 대상은 항상 통과
    private bool TryHoldSlot() =>
    !useAttackSlot || targetInfo == null || targetInfo.TryAcquire(this);

    /// <summary>
    /// 배치된 슬롯
    /// </summary>
    /// <returns></returns>
    private bool IsAtSlot()
    {
        if (!HasSlot) return true;

        Vector3 flat = GetChaseDestination() - transform.position;
        flat.y = 0f;
        return flat.sqrMagnitude <= slotTolerance * slotTolerance;
    }

    #endregion

    public void StopAI()
    {
        ReleaseSlot();
        if (aiCts == null) return;
        aiCts.Cancel();
        aiCts.Dispose();
        aiCts = null;
    }
}
