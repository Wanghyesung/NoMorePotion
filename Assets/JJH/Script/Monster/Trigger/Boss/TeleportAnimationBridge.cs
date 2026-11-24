using UnityEngine;

/// <summary>
/// 기존 몬스터 이벤트 + 텔포 이벤트 전달
/// </summary>
public class TeleportAnimationBridge : MonsterAnimationBridge
{
    private BossTeleport teleport;
    private MeteorRain meteor;

    protected override void Awake()
    {
        base.Awake();
        teleport = GetComponentInParent<BossTeleport>();
        meteor = GetComponentInParent<MeteorRain>();
    }

    private void TeleportBegin(AnimationEvent e) => teleport.Begin(e.floatParameter);
    private void TeleportVanish() => teleport.Vanish();
    private void TeleportArrive() => teleport.Arrive();
    private void MeteorCast()
    {
        if (combat.IsSkillCanceled) return;
        meteor.Cast();
    }
}