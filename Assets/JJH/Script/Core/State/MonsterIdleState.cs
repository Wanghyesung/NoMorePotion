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
        Debug.Log($"{mover.patrolTimerActive} : {mover.patrolDelay} | {mover.patrolTimer}");
        mover.patrolTimerActive = true;
        mover.patrolTimer = 0;
        Debug.Log($"{mover.patrolTimerActive} : {mover.patrolDelay} | {mover.patrolTimer}");
        mover.anim.IsWalk(false);
        mover.anim.IsRun(false);
    }

    public void OnStay()
    {
        if (mover.patrolDelay < mover.patrolTimer)
        {
            mover.patrolTimer = 0;
            mover.patrolTimerActive = false;
            mover.state.ChangeState(mover.stateGroup[MonsterState.Patrol]);
        }
    }
}
