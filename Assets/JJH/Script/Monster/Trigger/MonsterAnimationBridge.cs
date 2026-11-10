using UnityEngine;

public class MonsterAnimationBridge : MonoBehaviour
{
    protected MonsterCombat combat;
    protected virtual void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<MonsterCombat>();
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


}
