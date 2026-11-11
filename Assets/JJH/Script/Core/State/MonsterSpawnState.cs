using UnityEngine;

/// <summary>
/// 몬스터가 스폰되었을 때의 FSM
/// </summary>
public class MonsterSpawnState : IMonsterState
{
    private MonsterMove mover;

    public MonsterSpawnState(MonsterMove mover)
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
