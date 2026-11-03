using UnityEngine;
using UnityEngine.UIElements;
using static JJH_Enums;

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
        mover.anim.IsNormalAttack(false);
        mover.attackTimerActive = false;
        mover.agent.updateRotation = true;
    }

    public void OnStart()
    {
        mover.attackTimerActive = true;

        mover.agent.isStopped = true;
        mover.agent.velocity = Vector3.zero;
        mover.agent.updateRotation = false;

        mover.anim.IsRun(false);
        mover.anim.IsWalk(false);
        mover.anim.IsNormalAttack();
        mover.attackTimer = 0;
        
    }

    public void OnStay()
    {
        // 타깃을 향한 회전값 계산
        Quaternion targetRotation = 
            Quaternion.LookRotation(mover.nowTarget.transform.position);

        mover.transform.rotation = 
            Quaternion.Slerp(mover.transform.rotation, 
            targetRotation, Time.deltaTime * 10);

        if (mover.attackSpeed < mover.attackTimer)
        {
            mover.attackTimer = 0;
            mover.anim.IsNormalAttack();
        }
    }
}
