using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class GuideEvent
{
    [SerializeField] private SOEventText eventText;
    [Tooltip("이 단계를 완료하는 조건입니다. 오브젝트 활성화는 Start Event와 End Event에 연결합니다.")]
    [SerializeField] private List<GuideEndEvent> endEvents = new();
    [SerializeField] private UnityEvent startEvent = new();
    [SerializeField] private UnityEvent endEvent = new();

    private readonly HashSet<GuideEndEvent> completedConditions = new();
    private bool entering;

    public event Action<GuideEvent> Completed;
    public SOEventText EventText => eventText;
    public bool IsActive { get; private set; }
    public bool IsCompleted { get; private set; }
    public int ClearCount => completedConditions.Count;
    public int RequiredCount => endEvents.Count;

    public void Enter()
    {
        if (IsActive)
            return;

        IsActive = true;
        IsCompleted = false;
        entering = true;
        completedConditions.Clear();

        // 모든 조건을 초기화한 다음 구독해야 즉시 완료되는 조건도 안전하게 집계됩니다.
        foreach (GuideEndEvent condition in endEvents)
            condition.Prepare(this);
        foreach (GuideEndEvent condition in endEvents)
        {
            if (!IsActive)
                break;
            condition.Begin();
        }

        if (!IsActive)
            return;

        startEvent.Invoke();
        entering = false;
        CheckCompleted();
    }

    public void NotifyCompleted(GuideEndEvent condition)
    {
        if (!IsActive || !endEvents.Contains(condition) || !condition.IsCompleted)
            return;
        if (completedConditions.Add(condition))
            CheckCompleted();
    }

    private void CheckCompleted()
    {
        if (!IsActive || IsCompleted || entering || ClearCount != RequiredCount)
            return;

        IsCompleted = true;
        Completed?.Invoke(this);
    }

    public void Exit()
    {
        if (!IsActive || !IsCompleted)
            return;

        IsActive = false;
        StopConditions();
        endEvent.Invoke();
    }

    public void Cancel()
    {
        if (!IsActive)
            return;
        IsActive = false;
        StopConditions();
        endEvent.Invoke();
    }

    private void StopConditions()
    {
        foreach (GuideEndEvent condition in endEvents)
            if (condition != null)
                condition.Stop();
    }

}
