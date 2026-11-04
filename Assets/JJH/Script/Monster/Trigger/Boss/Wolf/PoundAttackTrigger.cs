using UnityEngine;
using static CommonStruct;
using static JJH_Constants;
public class PoundAttackTrigger : MonsterAttackTrigger
{
    private WolfCombat combat;
    private void Awake()
    {
        combat = GetComponentInParent<WolfCombat>();
    }

    protected override AttackInfo GetAttackInfo()
    {
        BossSkill data = combat.GetBossSkill(POUND);

        if (data == null) return new AttackInfo();

        return new AttackInfo()
        {
            Damage = data.SkillDamage,
            Position = combat.transform.position
        };
    }
}
