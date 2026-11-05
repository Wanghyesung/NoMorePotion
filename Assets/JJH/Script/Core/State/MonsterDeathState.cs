using UnityEngine;

/// <summary>
/// 몬스터 사망 상태 FSM
/// </summary>
public class MonsterDeathState : IMonsterState
{
    private MonsterMove mover;

    public MonsterDeathState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnEnd()
    {
    }

    public void OnStart()
    {
        mover.agent.isStopped = true;
        mover.agent.ResetPath();
    }

    public void OnStay()
    {
    }
}
