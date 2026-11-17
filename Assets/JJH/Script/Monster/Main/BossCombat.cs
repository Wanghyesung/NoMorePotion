using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static JJH_Enums;

public class BossCombat : MonsterCombat
{
    protected override void InitData(int id, int hpOverride = -1)
    {
        BossData bossData = EnemyDataLoader.Instance.LoadBossData(id);
        MonsterTextData textData = EnemyDataLoader.Instance.LoadTextData(id);

        if (bossData == null || textData == null)
        {
            Debug.LogWarning($"[{name}] 보스 데이터 로드 실패 id={id} " +
                $"boss={(bossData != null)} text={(textData != null)}", this);
            return;
        }

        int hp = hpOverride > 0 ? hpOverride : bossData.HP;

        Dictionary<DamageType, int> immuneVal = (bossData.Immune ?? new Dictionary<int, int>())
            .Where(kv => Enum.IsDefined(typeof(DamageType), kv.Key))
            .ToDictionary(kv => (DamageType)kv.Key, kv => kv.Value);

        SetData(new BossMonster(textData.Name, hp, hp, bossData.AtkPoint, bossData.AttackPower,
            immuneVal, bossData.Speed, bossData.Id, 0,
            bossData.SpawnArea, textData.Description, bossData.BossSkills, bossData.DamageType));

        mover.AttackSelector.SetPatterns(BuildSkillPatterns(bossData.BossSkills));
    }

    /// <summary>
    /// 보스 스킬 데이터 → 선택기 패턴 (Scripted 스킬은 별도 로직이 발동시키므로 제외)
    /// 늑대 등 상속 클래스에서도 재사용
    /// </summary>
    protected static List<AttackPattern> BuildSkillPatterns(List<BossSkill> skills)
    {
        var patterns = new List<AttackPattern>();
        if (skills == null) return patterns;   // 스킬 0개 보스: 기본 공격만 사용

        foreach (BossSkill s in skills)
        {
            if (s.Scripted) continue;

            patterns.Add(new AttackPattern
            {
                Name = s.AnimationName,
                Kind = AttackKind.Skill,
                MinRange = s.MinRange,
                MaxRange = s.MaxRange,
                Cooldown = s.Cooldown,
                Weight = s.Weight,
            });
        }
        return patterns;
    }
}
