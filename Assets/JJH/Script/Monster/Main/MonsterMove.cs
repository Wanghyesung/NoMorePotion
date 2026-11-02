using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static JJH_Enums;

using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(NavMeshAgent))]
public class MonsterMove : MonoBehaviourPunCallbacks
{
    [Header("AI 네비게이션")]
    [SF] private NavMeshAgent agent;

    [Header("본체 관련")]
    

    [Header("인스펙터")]
    [SF] private GameObject nowTarget;

    public StateMachine state;

    public Dictionary<MonsterState, IMonsterState> stateGroup;
    private void Awake()
    {
        if(agent == null)
            agent = GetComponent<NavMeshAgent>();
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
    }

}
