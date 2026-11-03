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
    }

    public void OnStay()
    {
    }
}
