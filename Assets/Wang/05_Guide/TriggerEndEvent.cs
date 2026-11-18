using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class TriggerEndEvent : GuideEndEvent
{
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private bool localPlayerOnly = true;

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsListening || (targetLayers.value & (1 << other.gameObject.layer)) == 0)
            return;
        if (localPlayerOnly)
        {
            Player player = other.GetComponentInParent<Player>();
            if (player == null || !player.IsLocalPlayer)
                return;
        }
        Complete();
    }
}
