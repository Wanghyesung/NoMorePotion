using UnityEngine;

public class WolfAnimationBridge : MonsterAnimationBridge
{
    private WolfCombat wolf;

    protected override void Awake()
    {
        base.Awake();
        if (wolf == null)
            wolf = combat as WolfCombat;
    }

    private void PoundAttackAnimationStart() => wolf.PoundAttackAnimationStart();
    private void PoundWindup() => wolf.PoundWindup();
    private void PoundTakeoff() => wolf.PoundTakeoff();
    private void PoundAttackOn() => wolf.PoundAttackOn();
    private void PoundAttackOff() => wolf.PoundAttackOff();
    private void PoundAttackAnimationEnd() => wolf.PoundAttackAnimationEnd();
}
