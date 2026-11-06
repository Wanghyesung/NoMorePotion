using Cysharp.Threading.Tasks;
using Photon.Pun;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.AI;
using static JJH_Constants;
using static JJH_Enums;

using HIn = UnityEngine.HideInInspector;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterMove : MonoBehaviourPunCallbacks
{
    [Header("스크립트")]
    public MonsterAnimationController anim;
    public MonsterCombat combat;

    [Header("AI 네비게이션")]
    public NavMeshAgent agent;

    [Header("본체 관련")]
    public float nextMoveDist;
    [SF] private float attackRange;
    public SphereCollider chaseCollider;
    public float attackSpeed;
    public float patrolDelay = 4;
    public float walkSpeed = 1.9f;
    public float runSpeed = 3.8f;
    public float turnSpeed = 5;
    public float NowSpeed => agent.speed;

    [Header("도달 불가 판정")]
    [SF] private ReachabilityTracker reachability = new();

    [Header("인스펙터")]
    public GameObject nowTarget;
    
    [HIn] public StateMachine state;
    [HIn] public float patrolTimer = 0;
    [HIn] public bool patrolTimerActive;
    [HIn] public float attackTimer = 0;
    [HIn]public float AttackRange => attackRange;
    [HIn] public bool IsAnimation;

    public Dictionary<MonsterState, IMonsterState> stateGroup;
    private CancellationTokenSource aiCts;
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

        state = new();

        stateGroup = new Dictionary<MonsterState, IMonsterState>()
        {
            [MonsterState.Spawn] = new MonsterSpawnState(this),
            [MonsterState.Idle] = new MonsterIdleState(this),
            [MonsterState.Patrol] = new MonsterPatrolState(this),
            [MonsterState.Chase] = new MonsterChaseState(this),
            [MonsterState.Attack] = new MonsterAttackState(this),
            [MonsterState.Death] = new MonsterDeathState(this)
        };
    }

    private void Start()
    {
        RegisterStates();
/*
        // 이후 캐릭터 스폰 + 이동속도 받을 시 스폰으로 작동 위치 변경
        if (runSpeed == 0)
            StartAI(3.8f).Forget();
        else 
            StartAI(runSpeed).Forget();*/
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
    public virtual bool CanAcquireTarget(GameObject target) => reachability.CanAcquire(target);

    /// <summary>
    /// 개체의 회전 조절 여부
    /// </summary>
    /// <returns></returns>
    protected virtual bool ShouldFaceTarget()
    {
        return state != null && state.GetStateType() == MonsterState.Attack;
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
        
        StopAI();
        anim.AnimatorRebind();
        aiCts = new CancellationTokenSource();
        CancellationToken token = aiCts.Token;
        this.combat = combat;
        // 이전 풀 정보 초기화
        state = new();
        nowTarget = null;
        IsAnimation = false;
        patrolTimer = attackTimer = 0;
        patrolTimerActive = false;

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

        // 도달 불가 상태가 유예 시간을 넘기면 추적 해제
        if (nowTarget != null &&
            reachability.ShouldDrop(transform.position, nowTarget, FSM_TIMER / MIL_SEC_F))
            nowTarget = null;

        if (nowTarget == null)
        {
            if (patrolTimerActive)
            {
                result = stateGroup[MonsterState.Idle];
                nowState = MonsterState.Idle;
            }
            else
            {
                result = stateGroup[MonsterState.Patrol];
                nowState = MonsterState.Patrol;
            }

            if (state.GetStateType() == nowState)
                result = null;

            return result;
        }
        
        float distance = Vector3.Distance(transform.position, nowTarget.transform.position);
        
        // 공격 범위 안
        if (distance <= attackRange)
        {

            result = stateGroup[MonsterState.Attack];
            nowState = MonsterState.Attack;
        }

        // 공격 범위 밖이지만 감지 범위 안
        else if (distance <= chaseCollider.radius)
        {
            result = stateGroup[MonsterState.Chase];
            nowState = MonsterState.Chase;
        }
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

    public void StopAI()
    {
        if (aiCts == null) return;
        aiCts.Cancel();
        aiCts.Dispose();
        aiCts = null;
    }
}
