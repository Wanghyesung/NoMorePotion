using System.Collections.Generic;
using UnityEngine;
using SF = UnityEngine.SerializeField;

public class BossHudBinder : MonoBehaviour
{
    private readonly List<(BossMonster data, string name)> engaged = new();

    [SF] private MonsterHPHud hud;
    private BossMonster bound;

    private void OnEnable()
    {
        BossEvents.OnEngaged += OnEngaged;
        BossEvents.OnDisengaged += OnDisengaged;
    }

    private void OnDisable()
    {
        BossEvents.OnEngaged -= OnEngaged;
        BossEvents.OnDisengaged -= OnDisengaged;

        engaged.Clear();
        Unbind();
        hud.HideImmediate();
    }

    private void OnEngaged(BossMonster data, string bossName)
    {
        engaged.RemoveAll(e => e.data == data);
        engaged.Add((data, bossName));
        Bind(data, bossName);
    }

    private void OnDisengaged(BossMonster data)
    {
        engaged.RemoveAll(e => e.data == data);
        if (data != bound) return; 

        bool died = bound.HP <= 0;
        Unbind();

        // 남은 보스가 있으면 그쪽으로 전환
        if (engaged.Count > 0)
        {
            var next = engaged[^1];
            Bind(next.data, next.name);
            return;
        }

        // 사망이면 HUD가 자체 사망 페이드를 재생 중이라 건드리지 않음
        if (!died) hud.FadeTo(0f);
    }

    private void Bind(BossMonster data, string bossName)
    {
        Unbind();
        bound = data;
        bound.OnHPChanged += hud.HealthChange;
        bound.OnCounterAccumed += hud.CounterValueChange;
        hud.Init(bound.MaxHP, bound.HP, bossName, data.MinCounter);
    }

    private void Unbind()
    {
        if (bound == null) return;
        bound.OnHPChanged -= hud.HealthChange;
        bound.OnCounterAccumed -= hud.CounterValueChange;
        bound = null;
    }
}
