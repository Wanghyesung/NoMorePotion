using UnityEngine;
using UnityEngine.UIElements;
using static JJH_Enums;
using static JJH_Constants;

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
        mover.attackTimer = 2;
    }

    public void OnStay()
    {
        float angleDeadzone = 1.5f;
        Vector3 targetDirection = 
            mover.nowTarget.transform.position - mover.transform.position;

        Quaternion targetRotation = 
            Quaternion.LookRotation(targetDirection);

        float angleDifference = Vector3.Angle(mover.transform.forward, targetDirection);

        if (angleDifference > angleDeadzone)
            mover.transform.rotation = 
            Quaternion.Slerp(mover.transform.rotation, 
            targetRotation, FSM_TIMER / MIL_SEC_F * 10);
        else
        {
            mover.transform.rotation = targetRotation;
        }
        if (mover.attackSpeed < mover.attackTimer)
        {
            mover.attackTimer = 0;
            mover.anim.IsNormalAttack();
        }
    }
}
