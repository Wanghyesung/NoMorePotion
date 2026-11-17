using UnityEngine;

public class MonsterAnimationBridge : MonoBehaviour
{
    protected MonsterCombat combat;
    protected MonsterVfx vfx;
    protected virtual void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<MonsterCombat>();
        if (vfx == null)
            vfx = GetComponentInParent<MonsterVfx>();
    }

    // 일반 공격
    private void NormalAttackAnimationStart() => combat.NormalAttackAnimationStart();
    private void NormalAttackOn() => combat.NormalAttackOn();
    private void NormalAttackOff() => combat.NormalAttackOff();
    private void NormalAttackAnimationEnd() => combat.NormalAttackAnimationEnd();

    // 스킬
    private void SkillAnimationStart() => combat.SkillAnimationStart();
    private void SkillHitOn(string skillName) => combat.SkillHitOn(skillName);
    private void SkillHitOff(string skillName) => combat.SkillHitOff(skillName);
    private void SkillAnimationEnd(string skillName) => combat.SkillAnimationEnd(skillName);
    private void WarningOn(AnimationEvent e)
    {
        if(vfx != null)
            vfx.ShowWarning(e.intParameter, e.floatParameter);
    }

    private void WarningOff(AnimationEvent e) 
    {
        if (vfx != null)
            vfx.HideWarning(e.intParameter); 
    }
    private void HitEffect(AnimationEvent e)
    {
        if (vfx != null)
            vfx.PlayHit(e.intParameter);
    }

    // 사망
    private void DeathAnimationEnd() => combat.DeathAnimationEnd();
}
