using Enzeun.Runtime;
using UnityEngine;

public sealed class DepletedEndEvent : GuideEndEvent
{
    [SerializeField] private WorldObject worldObject;
    private WorldObject subscribedObject;

    private void Reset() => worldObject = GetComponent<WorldObject>();

    protected override void OnBegin()
    {
        subscribedObject = worldObject;
        subscribedObject.OnDepleted += OnDepleted;
    }

    protected override void OnStop()
    {
        if (subscribedObject != null)
            subscribedObject.OnDepleted -= OnDepleted;
        subscribedObject = null;
    }

    private void OnDepleted(WorldObject target) => Complete();
}
