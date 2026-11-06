using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using static CommonStruct;
using static JJH_Constants;
using SF = UnityEngine.SerializeField;
public class SkillAttackTrigger : MonsterAttackTrigger
{
    [SF] private string skillName;

    protected override AttackInfo GetAttackInfo()
    {
        BossSkill skill = combat.GetBossSkill(skillName);
        if (skill == null)
        {
            Debug.LogWarning($"[{name}] 스킬 데이터 없음: {skillName}", this);
            return new AttackInfo();
        }

        BuffContainer buff = combat.GetStatusEffect();
        float atkRate = buff != null ? buff.GetStatMultiplier(StatType.AttackPower) : 1f;
        float damage = combat.MultiplierCalc(combat.GetDamage(), atkRate);

        return new AttackInfo
        {
            Damage = Mathf.RoundToInt(damage * skill.SkillDamage / 100f),
            Position = combat.transform.position
        };
    }
}
