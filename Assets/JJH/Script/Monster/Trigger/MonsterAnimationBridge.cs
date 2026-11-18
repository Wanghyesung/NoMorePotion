using UnityEngine;
using static JJH_Constants;
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
    private void SkillHitOn(AnimationEvent e) => combat.SkillHitOn(GetSkillNum(e));
    private void SkillHitOff(AnimationEvent e) => combat.SkillHitOff(GetSkillNum(e));
    private void SkillAnimationEnd(AnimationEvent e) => combat.SkillAnimationEnd(GetSkillNum(e));

    /// <summary>
    /// 이벤트 Int 칸 번호 우선, 비어 있으면 String("Skill2")에서 숫자 추출 (기존 클립 호환)
    /// </summary>
    private static int GetSkillNum(AnimationEvent e)
    {
        if (e.intParameter > 0) return e.intParameter;

        string s = e.stringParameter;
        if (!string.IsNullOrEmpty(s) && s.StartsWith(SKILL_PARAM_PREFIX) &&
            int.TryParse(s[SKILL_PARAM_PREFIX.Length..], out int num))
            return num;

        return 0;
    }

    private void WarningOn(AnimationEvent e)
    {
        if (combat.IsSkillCanceled) return;
        if (vfx != null)
            vfx.ShowWarning(e.intParameter, e.floatParameter);
    }


    private void WarningOff(AnimationEvent e) 
    {
        if (vfx != null)
            vfx.HideWarning(e.intParameter); 
    }

    private void HitEffect(AnimationEvent e)
    {
        if (combat.IsSkillCanceled) return;
        vfx?.PlayHit(e.intParameter);
    }

    private void CounterOn() => combat.CounterOn();
    private void CounterOff() => combat.CounterOff();
    private void CounterSuccessEnd() => combat.CounterSuccessEnd();

    // 사망
    private void DeathAnimationEnd() => combat.DeathAnimationEnd();
}
