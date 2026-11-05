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
        mover.agent.isStopped = false;
        mover.anim.IsNormalAttack(false);
        mover.agent.updateRotation = true;
    }

    public void OnStart()
    {
        mover.agent.isStopped = true;
        mover.agent.velocity = Vector3.zero;
        mover.agent.updateRotation = false;

        mover.anim.IsRun(false);
        mover.anim.IsWalk(false);
    }

    public void OnStay()
    {
        if (mover.attackSpeed < mover.attackTimer)
        {
            mover.attackTimer = -Random.Range(0f, 0.3f);
            mover.anim.IsNormalAttack();
        }
    }
}
