using Cysharp.Threading.Tasks;
using MoreMountains.Tools;
using Photon.Pun;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using static JJH_Constants;
using static JJH_Enums;

using SF = UnityEngine.SerializeField;
using HIn = UnityEngine.HideInInspector;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterMove : MonoBehaviourPunCallbacks
{
    [Header("AI 네비게이션")]
    public NavMeshAgent agent;

    [Header("본체 관련")]
    public float nextMoveDist;
    [SF] private float attackRange;
    [SF] private float attackSpeed;
    [SF] private float patrolDelay = 4;
    

    [Header("인스펙터")]
    [SF] private GameObject nowTarget;

    [HIn]public StateMachine state;
    private float patrolTimer;

    public Dictionary<MonsterState, IMonsterState> stateGroup;
    private void Awake()
    {
        if(agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (!PhotonNetwork.IsMasterClient)
        {
            if (agent != null) agent.enabled = false;

            if (TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
        }
    }

    private void Start()
    {
        state = new();

        stateGroup = new Dictionary<MonsterState, IMonsterState>()
        {
            [MonsterState.Patrol] = new MonsterPatrolState(this),
            [MonsterState.Chase] = new MonsterChaseState(this),
            [MonsterState.Attack] = new MonsterAttackState(this)
        };

        state.ChangeState(stateGroup[MonsterState.Patrol]);
    }

    // 혹시 공격 범위 갱신이 필요할 경우
    public void StartAI()
    {
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();

        StartAILoop(destroyToken).Forget();
    }

    private IMonsterState UpdateState()
    {
        IMonsterState result = null;

        if (nowTarget == null)
            result = stateGroup[MonsterState.Patrol];
        

        float distance = Vector3.Distance(transform.position, nowTarget.transform.position);

        // 공격 범위 안
        if (distance <= attackRange)
            result = stateGroup[MonsterState.Attack];
        
        // 공격 범위 밖이지만 감지 범위 안
        else if (distance <= nextMoveDist)
            result = stateGroup[MonsterState.Chase];
        else
        {
            nowTarget = null;
            result = stateGroup[MonsterState.Patrol];
        }

        if (state.GetStateType() == MonsterState.Patrol)
            result = null;

        return result;
    }

    private async UniTaskVoid StartAILoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            IMonsterState data = UpdateState();

            if (data != null)
                state.ChangeState(data);

            state.Stay();

            await UniTask.Delay(FSM_TIMER, cancellationToken: token);
        }
    }
}
