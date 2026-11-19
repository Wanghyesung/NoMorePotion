using UnityEngine;

public sealed class PotionHitEndEvent : GuideEndEvent
{
    protected override void OnBegin() => ParabolicMovement.OnPotionHit += OnPotionHit;

    protected override void OnStop() => ParabolicMovement.OnPotionHit -= OnPotionHit;

    private void OnPotionHit(Collider hit)
    {
        if (hit.transform.IsChildOf(transform))
            Complete();
    }
}
