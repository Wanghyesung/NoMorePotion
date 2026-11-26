using System.Collections.Generic;
using UnityEngine;
using SF = UnityEngine.SerializeField;

public class BossHudBinder : MonoBehaviour
{
    private class Entry
    {
        public BossMonster Data;
        public string Name;
        public Transform Owner;
        public MonsterMove Mover;
    }

    private readonly List<Entry> engaged = new();

    [SF] private MonsterHPHud hud;
    // 보스 허드 표시 거리 제한 길이
    [SF] private float showRange = 50f;
    // 허드 갱신 주기
    [SF] private float refreshInterval = 0.25f;

    private BossMonster bound;
    private float timer;

    private void Update()
    {
        if (engaged.Count == 0) return;

        timer += Time.deltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;

        Refresh();
    }

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


    private void OnEngaged(BossMonster data, string bossName, Transform owner)
    {
        engaged.RemoveAll(e => e.Data == data);
        engaged.Add(new Entry
        {
            Data = data,
            Name = bossName,
            Owner = owner,
            Mover = owner != null ? owner.GetComponent<MonsterMove>() : null
        });

        Refresh();
    }

    private void OnDisengaged(BossMonster data)
    {
        engaged.RemoveAll(e => e.Data == data);
        if (data != bound) return;

        bool died = bound.HP <= 0;
        Unbind();

        if (!died) hud.FadeTo(0f);

        Refresh();   // 다른 관련 보스가 있으면 이어서 표시
    }

    /// <summary>
    /// 내 플레이어 기준으로 표시할 보스를 고름
    /// </summary>
    private void Refresh()
    {
        PlayerRegistryMember me = PlayerRegistry.Local;
        Entry best = null;
        float bestScore = float.MaxValue;
        bool boundStillRelevant = false;

        foreach (Entry e in engaged)
        {
            if (e.Owner == null || e.Data.HP <= 0) continue;

            bool targetingMe = me != null && e.Mover != null && e.Mover.nowTarget == me.gameObject;
            float dist = me != null ? Vector3.Distance(me.transform.position, e.Owner.position) : 0f;
            if (!targetingMe && dist > showRange) continue;

            if (e.Data == bound) boundStillRelevant = true;

            // 나를 노리는 보스가 최우선, 그다음 가까운 순
            float score = targetingMe ? -1f : dist;
            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        // 관련된 보스가 없으면 숨김
        if (best == null)
        {
            if (bound != null)
            {
                Unbind();
                hud.FadeTo(0f);
            }
            return;
        }

        // 지금 띄운 보스가 여전히 관련 있으면, 나를 노리는 보스가 새로 생긴 경우만 교체 (깜빡임 방지)
        if (best.Data == bound) return;
        if (boundStillRelevant && bestScore >= 0f) return;

        Bind(best.Data, best.Name);
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
