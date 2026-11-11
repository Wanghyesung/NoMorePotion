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
        // 공용 공격 간격 (스킬/기본 공격 사이의 최소 텀)
        if (mover.attackTimer < mover.attackSpeed || mover.nowTarget == null) return;

        float distance = mover.DistanceToTarget();

        // 1순위: 쿨타임이 찬 스킬
        AttackPattern pattern = mover.AttackSelector.HasPatterns
            ? mover.AttackSelector.PatternSelect(distance)
            : null;

        if (pattern != null)
        {
            mover.AttackSelector.MarkUsed(pattern);
            mover.anim.PlayAttack(pattern);
        }
        // 2순위: 기본 공격 (사거리 안일 때만)
        else if (distance <= mover.AttackRange)
        {
            mover.anim.IsNormalAttack();
        }
        else return;   // 스킬 사거리였지만 방금 다 써버림 → 다음 틱에 추적으로 전환

        mover.attackTimer = -Random.Range(0f, 0.3f);
    }
}
