using UnityEngine;
using UnityEngine.Events;

public abstract class GuideEndEvent : MonoBehaviour
{
    [SerializeField] private UnityEvent startEvent = new();
    [SerializeField] private UnityEvent endEvent = new();

    private GuideEvent owner;
    public bool IsListening { get; private set; }
    public bool IsCompleted { get; private set; }

    internal void Prepare(GuideEvent guideEvent)
    {
        Stop();
        owner = guideEvent;
        IsCompleted = false;
    }

    public void Begin()
    {
        if (!owner.IsActive || IsListening || IsCompleted)
            return;

        IsListening = true;
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
        OnStop();
    }

    protected virtual void OnBegin() { }
    protected virtual void OnStop() { }
    protected virtual void OnDisable() => Stop();
}
