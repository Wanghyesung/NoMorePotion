using UnityEngine;

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
    }

    public void OnStart()
    {
    }

    public void OnStay()
    {
    }
}
