using UnityEngine;

public sealed class NightWaveEndEvent : GuideEndEvent
{
    [SerializeField] private NightWaveSpawner spawner;
    private NightWaveSpawner subscribedSpawner;

    protected override void OnBegin()
    {
        subscribedSpawner = spawner;
        subscribedSpawner.OnAllMonstersDead += Complete;
    }

    protected override void OnStop()
    {
        if (subscribedSpawner != null)
            subscribedSpawner.OnAllMonstersDead -= Complete;
        subscribedSpawner = null;
    }
}
