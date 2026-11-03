using UnityEngine;

using static JJH_Enums;
/// <summary>
/// 몬스터 대기 상태 FSM
/// </summary>
public class MonsterIdleState : IMonsterState
{
    private MonsterMove mover;

    public MonsterIdleState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnEnd()
    {
        mover.patrolTimerActive = false;
    }

    public void OnStart()
    {
        mover.patrolTimerActive = true;
        mover.anim.IsWalk(false);
        mover.anim.IsRun(false);
    }

    public void OnStay()
    {
        Debug.Log(mover.patrolDelay);
        Debug.Log(mover.patrolTimer);
        if (mover.patrolDelay < mover.patrolTimer)
        {
            mover.patrolTimer = 0;
            mover.patrolTimerActive = false;
            mover.state.ChangeState(mover.stateGroup[MonsterState.Patrol]);
        }
    }
}
