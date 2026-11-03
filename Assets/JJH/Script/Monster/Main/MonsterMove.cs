using Cysharp.Threading.Tasks;
using Photon.Pun;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using static JJH_Constants;
using static JJH_Enums;

using HIn = UnityEngine.HideInInspector;
using SF = UnityEngine.SerializeField;

[DrawWithUnity]
[RequireComponent(typeof(NavMeshAgent))]
public class MonsterMove : MonoBehaviourPunCallbacks
{
    [Header("스크립트")]
    public MonsterAnimationController anim;

    [Header("AI 네비게이션")]
    public NavMeshAgent agent;

    [Header("본체 관련")]
    public float nextMoveDist;
    [SF] private float attackRange;
    public SphereCollider chaseCollider;
    public float attackSpeed;
    public float patrolDelay = 4;
    public float walkSpeed = 2;
    public float runSpeed = 4;
    public float turnSpeed = 5;

    [Header("인스펙터")]
    public GameObject nowTarget;
    

    [HIn] public StateMachine state;
    [HIn] public float patrolTimer = 0;
    [HIn] public bool patrolTimerActive;
    [HIn] public float attackTimer = 0;
    [HIn] public bool attackTimerActive;
    [HIn] public bool IsAnimation;

    public Dictionary<MonsterState, IMonsterState> stateGroup;
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
    }

    private void Start()
    {
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

        StartAI().Forget();
    }

    private void Update()
    {
        if (!IsAnimation && state.GetStateType() == MonsterState.Attack)
            AttackAngleRotate();
    }

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

    // 혹시 공격 범위 갱신이 필요할 경우
    public async UniTask StartAI()
    {
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();

        StartAILoop(destroyToken).Forget();
        await UniTask.Delay(FSM_TIMER);

        state.ChangeState(stateGroup[MonsterState.Patrol]);
    }

    private IMonsterState UpdateState()
    {
        IMonsterState result = null;
        MonsterState nowState = MonsterState.None;

        //스폰 중엔 판단 정지
        if (state.GetStateType() == MonsterState.Spawn) return null;

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

                if (attackTimerActive)
                    attackTimer += FSM_TIMER / MIL_SEC_F;

                state.Stay();
            }

            
            await UniTask.Delay(FSM_TIMER, cancellationToken: token);
        }
    }
}
