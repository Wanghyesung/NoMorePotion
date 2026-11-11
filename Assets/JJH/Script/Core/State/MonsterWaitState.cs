using UnityEngine;


/// <summary>
/// 타겟의 공격 슬롯이 없을 때 대기하는 상태 FSM
/// </summary>
public class MonsterWaitState : IMonsterState
{
    private const float ArriveDistance = 0.5f;
    private readonly MonsterMove mover;

    public MonsterWaitState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnStart()
    {
        mover.agent.speed = mover.walkSpeed;
        mover.anim.IsRun(false);
    }

    public void OnStay()
    {
        if (mover.nowTarget == null) return;

        Vector3 dest = mover.GetWaitPoint();
        Vector3 flat = dest - mover.transform.position;
        flat.y = 0f;

        // 아직 슬롯에 지정되었으나 제자리에 배치되지 않은 경우
        if (flat.sqrMagnitude > ArriveDistance * ArriveDistance)
        {
            mover.agent.isStopped = false;
            mover.agent.updateRotation = true;
            mover.agent.SetDestination(dest);
            mover.anim.IsWalk(true);
        }
        // 위치에 도달한 경우 멈춰 대상을 주시
        else
        { 
            mover.agent.isStopped = true;
            mover.agent.velocity = Vector3.zero;
            mover.agent.updateRotation = false;
            mover.anim.IsWalk(false);
        }
    }

    public void OnEnd()
    {
        mover.agent.isStopped = false;
        mover.agent.updateRotation = true;
        mover.anim.IsWalk(false);
    }
}
