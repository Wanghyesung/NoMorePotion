using UnityEngine;

/// <summary>
/// 몬스터 공격 상태 FSM
/// </summary>
public class MonsterAttackState : IMonsterState
{
    private MonsterMove mover;

    public MonsterAttackState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnEnd()
    {
    }

    public void OnStart()
    {
        mover.agent.isStopped = true;
        mover.agent.velocity = Vector3.zero;
        mover.anim.IsRun(false);
        mover.anim.IsWalk(false);
    }

    public void OnStay()
    {
    }
}
