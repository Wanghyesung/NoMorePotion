/// <summary>
/// 타겟의 공격 슬롯이 없을 때 대기하는 상태 FSM
/// </summary>
public class MonsterWaitState : IMonsterState
{
    private readonly MonsterMove mover;

    public MonsterWaitState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnStart()
    {
        mover.agent.isStopped = true;
        mover.agent.velocity = UnityEngine.Vector3.zero;
        mover.agent.updateRotation = false;

        mover.anim.IsRun(false);
        mover.anim.IsWalk(false);
    }

    public void OnStay()
    {
        
    }

    public void OnEnd()
    {
        mover.agent.isStopped = false;
        mover.agent.updateRotation = true;
    }
}
