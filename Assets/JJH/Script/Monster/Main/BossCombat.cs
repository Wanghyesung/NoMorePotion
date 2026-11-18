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
        List<BossSkill> skills = EnemyDataLoader.Instance.LoadSkills(id);

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
            bossData.SpawnArea, textData.Description, skills, bossData.DamageType));

        mover.AttackSelector.SetPatterns(AttackSelector.BuildSkillPatterns(skills));
    }
}
