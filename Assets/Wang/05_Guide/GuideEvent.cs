using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[Serializable]
public sealed class GuideEvent
{
    [SerializeField] private SOEventText eventText;
    [Tooltip("이 단계를 완료하는 조건입니다. 시작 시 오브젝트를 켜고 종료 시 끕니다.")]
    [FormerlySerializedAs("endEvents")]
    [SerializeField] private List<GuideEndEvent> events = new();
    [SerializeField] private UnityEvent startEvent = new();
    [SerializeField] private UnityEvent endEvent = new();

    private readonly HashSet<GuideEndEvent> pendingEvents = new();
    private GuideManager manager;

    public SOEventText EventText => eventText;
    public IReadOnlyList<GuideEndEvent> Events => events;
    [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
    public int RemainingCount => pendingEvents.Count;

    public void Enter(GuideManager guideManager)
    {
        manager = guideManager;
        pendingEvents.Clear();
        foreach (GuideEndEvent condition in events)
            pendingEvents.Add(condition);

        foreach (GuideEndEvent condition in events)
            condition.Prepare(this, manager);

        SetObjectsActive(true);
        startEvent.Invoke();
        if (pendingEvents.Count == 0)
        {
            manager.NotifyCompleted(this);
            return;
        }

        foreach (GuideEndEvent condition in events)
            condition.Begin();
    }

    public void NotifyCompleted(GuideEndEvent condition)
    {
        if (!pendingEvents.Remove(condition))
            return;

        if (pendingEvents.Count == 0)
            manager.NotifyCompleted(this);
    }

    public void Exit()
    {
        StopConditions();
        pendingEvents.Clear();
        SetObjectsActive(false);
        endEvent.Invoke();
    }

    public void Cancel() => Exit();

    internal void SetObjectsActive(bool active)
    {
        foreach (GuideEndEvent condition in events)
            if (condition != null)
                condition.gameObject.SetActive(active);
    }

    private void StopConditions()
    {
        foreach (GuideEndEvent condition in events)
            if (condition != null)
                condition.Stop();
    }

}
