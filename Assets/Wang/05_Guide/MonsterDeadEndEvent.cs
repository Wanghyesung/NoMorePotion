using UnityEngine;

public sealed class MonsterDeadEndEvent : GuideEndEvent
{
    [SerializeField] private MonsterCombat monster;
    private MonsterCombat subscribedMonster;

    private void Reset() => monster = GetComponent<MonsterCombat>();

    protected override void OnBegin()
    {
        subscribedMonster = monster;
        subscribedMonster.Died += OnDied;
    }

    protected override void OnStop()
    {
        if (subscribedMonster != null)
            subscribedMonster.Died -= OnDied;
        subscribedMonster = null;
    }

    private void OnDied(MonsterCombat target) => Complete();
}
