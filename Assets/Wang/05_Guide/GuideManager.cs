using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class GuideManager : MonoBehaviour
{
    [Tooltip("각 단계 시작 전 대기 시간(초). 0이면 즉시 시작하며, 대기는 Time.timeScale과 무관합니다.")]
    [SerializeField, Min(0f)] private float enterDelaySeconds;
    [Tooltip("각 항목이 하나의 가이드 단계입니다. 설명, 생성할 오브젝트와 종료 조건을 항목 안에서 설정합니다.")]
    [SerializeField] private List<GuideEvent> events = new();
    [SerializeField] private GuideDescriptionPanel descriptionPanel;
    [SerializeField] private UnityEvent startEvent = new();
    [SerializeField] private UnityEvent completedEvent = new();

    private int currentIndex;
    private CancellationTokenSource enterCancellation;

    public GuideEvent CurrentEvent { get; private set; }
    public bool IsRunning { get; private set; }

    private void Start()
    {
        BeginGuide();
    }

    public void BeginGuide()
    {
        if (IsRunning)
            return;

        IsRunning = true;
        currentIndex = 0;
        startEvent.Invoke();
        if (IsRunning)
            Enter().Forget();
    }

    private async UniTask Enter()
    {
        if (enterDelaySeconds > 0f)
        {
            enterCancellation ??= new CancellationTokenSource();
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(enterDelaySeconds),
                ignoreTimeScale: true, cancellationToken: enterCancellation.Token)
                .SuppressCancellationThrow();
            if (canceled)
                return;
        }

        CurrentEvent = events[currentIndex];
        descriptionPanel.Completed += StartCurrentEvent;
        descriptionPanel.Show(CurrentEvent.EventText);
    }

    private void StartCurrentEvent()
    {
        descriptionPanel.Completed -= StartCurrentEvent;
        CurrentEvent.Completed += Exit;
        CurrentEvent.Enter();
    }

    private void Exit(GuideEvent step)
    {
        if (!IsRunning || step != CurrentEvent)
            return;

        step.Completed -= Exit;
        step.Exit();
        if (!IsRunning)
            return;

        CurrentEvent = null;
        currentIndex++;
        if (currentIndex >= events.Count)
        {
            IsRunning = false;
            CancelPendingEnter();
            completedEvent.Invoke();
            return;
        }

        Enter().Forget();
    }

    public void StopGuide()
    {
        if (!IsRunning)
            return;
        IsRunning = false;
        CancelPendingEnter();
        descriptionPanel.Completed -= StartCurrentEvent;
        descriptionPanel.Stop();
        if (CurrentEvent != null)
        {
            CurrentEvent.Completed -= Exit;
            CurrentEvent.Cancel();
            CurrentEvent = null;
        }
    }

    private void CancelPendingEnter()
    {
        enterCancellation?.Cancel();
        enterCancellation?.Dispose();
        enterCancellation = null;
    }

    private void OnDisable() => StopGuide();
}
