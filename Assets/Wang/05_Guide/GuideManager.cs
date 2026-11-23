using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class GuideManager : MonoBehaviour
{
    [Tooltip("각 단계 시작 전 대기 시간(초). 0이면 즉시 시작하며, 대기는 Time.timeScale과 무관합니다.")]
    [SerializeField, Min(0f)] private float enterDelaySeconds;
    [Tooltip("각 항목이 하나의 가이드 단계입니다. 설명과 종료 조건을 설정하며 조건 오브젝트는 자동으로 켜고 끕니다.")]
    [SerializeField] private List<GuideEvent> events = new();
    [SerializeField] private GuideDescriptionPanel descriptionPanel;
    [SerializeField] private GuideArrow guideArrow;
    [SerializeField] private RectTransform panelKeyAnchor;
    [SerializeField] private UnityEvent startEvent = new();
    [SerializeField] private UnityEvent completedEvent = new();

    private int currentIndex;
    private CancellationTokenSource enterCancellation;
    private IDisposable clockStartSubscription;
    private GuideEndEvent panelKeyEvent;
    private GuideEndEvent playerKeyEvent;

    public GuideEvent CurrentEvent { get; private set; }
    public RectTransform PanelKeyAnchor => panelKeyAnchor;
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
        HideGuideArrow();
        foreach (GuideEvent step in events)
            step.SetObjectsActive(false);
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
        KeyCanvas.Instance.ShowPanelKey(KeyGuide.Enter, panelKeyAnchor);
        descriptionPanel.Show(CurrentEvent.EventText);
    }

    private void StartCurrentEvent()
    {
        descriptionPanel.Completed -= StartCurrentEvent;
        KeyCanvas.Instance.HidePanelKey();
        CurrentEvent.Enter(this);
    }

    public void NotifyCompleted(GuideEvent step) => Exit(step);

    public void StopClock()
    {
        clockStartSubscription?.Dispose();
        clockStartSubscription = null;
        if (!GameAuthority.HasAuthority)
            return;

        TimeManager.Instance.StopClock();
        // 세션이 뒤늦게 시계를 시작해도 튜토리얼에서는 정지를 유지한다.
        clockStartSubscription = GameSessionManager.Instance.Phase
            .Where(phase => phase == SessionPhase.Playing)
            .Take(1)
            .Subscribe(_ => TimeManager.Instance.StopClock());
    }

    public void StartClock()
    {
        clockStartSubscription?.Dispose();
        clockStartSubscription = null;
        if (GameAuthority.HasAuthority)
            TimeManager.Instance.StartClock();
    }

    private void Exit(GuideEvent step)
    {
        if (!IsRunning || step != CurrentEvent)
            return;

        HideGuideArrow();
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
        clockStartSubscription?.Dispose();
        clockStartSubscription = null;
        if (!IsRunning)
            return;
        IsRunning = false;
        if (KeyCanvas.Instance != null)
        {
            KeyCanvas.Instance.HidePanelKey();
            KeyCanvas.Instance.HidePlayerKey();
        }
        HideGuideArrow();
        CancelPendingEnter();
        descriptionPanel.Completed -= StartCurrentEvent;
        descriptionPanel.Stop();
        if (CurrentEvent != null)
        {
            CurrentEvent.Cancel();
            CurrentEvent = null;
        }
    }

    internal void ShowGuideKey(GuideEndEvent condition)
    {
        if (condition.GuideKey == KeyGuide.None)
            return;

        if (condition.KeyPosition == GuideKeyPosition.Panel)
        {
            panelKeyEvent = condition;
            KeyCanvas.Instance.ShowPanelKey(condition.GuideKey, panelKeyAnchor);
        }
        else
        {
            playerKeyEvent = condition;
            KeyCanvas.Instance.ShowPlayerKey(condition.GuideKey);
        }
    }

    internal void HideGuideKey(GuideEndEvent condition)
    {
        if (panelKeyEvent == condition)
        {
            panelKeyEvent = null;
            if (KeyCanvas.Instance != null)
                KeyCanvas.Instance.HidePanelKey();
        }

        if (playerKeyEvent == condition)
        {
            playerKeyEvent = null;
            if (KeyCanvas.Instance != null)
                KeyCanvas.Instance.HidePlayerKey();
        }
    }

    public void ShowGuideArrow(Transform target)
    {
        Transform playerView = GameSessionManager.Instance.LocalPlayer.GetComponent<FSM>().PlayerView;
        guideArrow.transform.SetParent(playerView, false);
        guideArrow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        guideArrow.SetTargetRotate(target);
        guideArrow.gameObject.SetActive(true);
    }

    public void HideGuideArrow()
    {
        if (guideArrow != null)
            guideArrow.gameObject.SetActive(false);
    }

    private void CancelPendingEnter()
    {
        enterCancellation?.Cancel();
        enterCancellation?.Dispose();
        enterCancellation = null;
    }

    private void OnDisable() => StopGuide();
}
