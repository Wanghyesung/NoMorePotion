using UnityEngine;

public class MonsterPatrolState : IMonsterState
{
    private MonsterMove mover;

    public MonsterPatrolState(MonsterMove mover)
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
