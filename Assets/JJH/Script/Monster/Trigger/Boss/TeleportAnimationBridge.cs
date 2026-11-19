using UnityEngine;

/// <summary>
/// 기존 몬스터 이벤트 + 텔포 이벤트 전달 (WolfAnimationBridge와 같은 방식)
/// </summary>
public class TeleportAnimationBridge : MonsterAnimationBridge
{
    private BossTeleport teleport;

    protected override void Awake()
    {
        base.Awake();
        teleport = GetComponentInParent<BossTeleport>();
    }

    private void TeleportBegin(AnimationEvent e) => teleport.Begin(e.floatParameter);
    private void TeleportVanish() => teleport.Vanish();
    private void TeleportArrive() => teleport.Arrive();
}