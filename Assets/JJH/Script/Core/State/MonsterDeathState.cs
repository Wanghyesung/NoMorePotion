using UnityEngine;

public class MonsterDeathState : MonoBehaviour
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
    }

    public void OnStay()
    {
    }
}
