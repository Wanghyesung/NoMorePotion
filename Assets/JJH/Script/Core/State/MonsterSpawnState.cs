using UnityEngine;

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
