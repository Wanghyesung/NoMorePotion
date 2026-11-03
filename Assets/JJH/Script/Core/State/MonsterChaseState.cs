using UnityEngine;

/// <summary>
/// 몬스터 추격 상태 FSM
/// </summary>
public class MonsterChaseState : IMonsterState
{
    private MonsterMove mover;

    public MonsterChaseState(MonsterMove mover)
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
