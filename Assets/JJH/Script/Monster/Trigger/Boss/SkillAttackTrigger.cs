using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using static CommonStruct;
using static JJH_Constants;
using SF = UnityEngine.SerializeField;
public class SkillAttackTrigger : MonsterAttackTrigger
{
    [SF] private int skillNum;

    protected override AttackInfo GetAttackInfo()
    {
        BossSkill skill = combat.GetBossSkill(skillNum);
        if (skill == null)
        {
            Debug.LogWarning($"[{name}] 스킬 데이터 없음: {skillNum}", this);
            return new AttackInfo();
        }

        return combat.CreateSkillAttackInfo(skillNum);
    }

    // SkillAttackTrigger
    protected override void OnHit(Collider other)
    {
        BossSkill skill = combat.GetBossSkill(skillNum);
        if (skill?.StatusEffect == null) return;

        MonsterCombat.ApplyStatusTo(other, skill.StatusEffect, skill,
            skill.StatusPercent, skill.StatusDuration);
    }
}
