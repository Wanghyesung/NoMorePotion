using UnityEngine;
using UnityEngine.Events;

public enum GuideKeyPosition
{
    Player,
    Panel
}

public abstract class GuideEndEvent : MonoBehaviour
{
    [Tooltip("조건이 진행되는 동안 표시할 키입니다. None이면 표시하지 않습니다.")]
    [SerializeField] private KeyGuide guideKey = KeyGuide.None;
    [SerializeField] private GuideKeyPosition keyPosition;
    [Tooltip("조건이 시작되면 가이드 화살표가 이 오브젝트를 가리킵니다.")]
    [SerializeField] private bool guideArrow;
    [SerializeField] private UnityEvent startEvent = new();
    [SerializeField] private UnityEvent endEvent = new();

    private GuideEvent owner;
    private GuideManager manager;
    public bool IsListening { get; private set; }
    public bool IsCompleted { get; private set; }
    public KeyGuide GuideKey => guideKey;
    public GuideKeyPosition KeyPosition => keyPosition;

    internal void Prepare(GuideEvent guideEvent, GuideManager guideManager)
    {
        Stop();
        owner = guideEvent;
        manager = guideManager;
        IsCompleted = false;
    }

    public void Begin()
    {
        if (IsListening || IsCompleted)
            return;

        IsListening = true;
        manager.ShowGuideKey(this);
        if (guideArrow)
            manager.ShowGuideArrow(transform);
        OnBegin();
        if (IsListening)
            startEvent.Invoke();
    }

    public void Complete()
    {
        if (!IsListening)
            return;

        GuideEvent currentOwner = owner;
        IsCompleted = true;
        Stop();
        endEvent.Invoke();
        if (currentOwner != null)
            currentOwner.NotifyCompleted(this);
    }

    public void Stop()
    {
        if (!IsListening)
            return;

        IsListening = false;
        manager.HideGuideKey(this);
        OnStop();
    }

    protected void HideGuideKey() => manager.HideGuideKey(this);

    protected virtual void OnBegin() { }
    protected virtual void OnStop() { }
    protected virtual void OnDisable() => Stop();
}
