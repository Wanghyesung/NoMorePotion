using Unity.VisualScripting;
using UnityEngine;
using static JJH_Enums;

/// <summary>
/// 몬스터 추격 상태 FSM
/// </summary>
public class MonsterChaseState : IMonsterState
{
    private MonsterMove mover;

    public MonsterChaseState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnEnd()
    {
        if (mover.state.GetStateType() == MonsterState.Patrol)
            mover.nowTarget = null;
    }

    public void OnStart()
    {
        mover.agent.ResetPath();
        mover.agent.isStopped = false;
        mover.agent.speed = mover.runSpeed;
        mover.anim.IsRun();
        mover.anim.IsWalk(false);
        mover.patrolTimer = 0;
        mover.patrolTimerActive = false;
    }

    public void OnStay()
    {
        mover.agent.SetDestination(mover.nowTarget.transform.position);
    }
}
