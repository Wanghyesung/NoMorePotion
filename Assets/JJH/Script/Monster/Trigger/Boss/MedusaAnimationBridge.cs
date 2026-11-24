using UnityEngine;

public class MedusaAnimationBridge : MonsterAnimationBridge
{
    private UseProjectile spread;

    protected override void Awake()
    {
        base.Awake();

        spread = GetComponentInParent<UseProjectile>();
    }

    private void SpreadFire(AnimationEvent e) => spread.SpreadFire(e.intParameter);
}
