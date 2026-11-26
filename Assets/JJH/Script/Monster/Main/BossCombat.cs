using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Photon.Pun;

using static JJH_Enums;

using SF = UnityEngine.SerializeField;

public class BossCombat : MonsterCombat
{
    [Header("보스 체력바")]
    [Tooltip("마지막 피격 후 체력바 유지 시간")]
    [SF] private float engageHoldTime = 5f;

    private bool bossEngaged;
    private float lastHitTime = -999f;

    protected override void OnEnable()
    {
        base.OnEnable();
        Damaged += OnBossDamaged;
        Died += OnBossDied;

        bossEngaged = false;      // 새 생애는 미참전 상태로 시작
        lastHitTime = -999f;
    }

    protected override void OnDisable()
    {
        Damaged -= OnBossDamaged;
        Died -= OnBossDied;
        SetEngaged(false);
        base.OnDisable();
    }

    private void Update()
    {
        if (!GameAuthority.HasAuthority) return;

        NormalMonster d = GetData();
        if (d == null || d.HP <= 0) return;

        // 타겟이 있거나 최근에 맞았으면 체력바 표시
        SetEngaged(mover.nowTarget != null || Time.time < lastHitTime + engageHoldTime);
    }

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
            bossData.SpawnArea, textData.Description, skills, bossData.DamageType, bossData.MinCounter));
        minCounterDamage = bossData.MinCounter;
        mover.AttackSelector.SetPatterns(AttackSelector.BuildSkillPatterns(skills));
    }

    private void OnBossDamaged(MonsterCombat _) => lastHitTime = Time.time;

    /// <summary>
    /// 사망은 모든 클라이언트에서 실행되므로 RPC 없이 각자 해제
    /// (HUD는 HP 0을 보고 자체 사망 연출 재생)
    /// </summary>
    private void OnBossDied(MonsterCombat _)
    {
        if (bossEngaged) ApplyEngaged(false);
    }

    #region 체력바 연동
    private void SetEngaged(bool value)
    {
        if (bossEngaged == value || GetData() == null) return;

        ApplyEngaged(value);

        if (PhotonNetwork.InRoom && GameAuthority.HasAuthority)
            photonView.RPC(nameof(RpcBossEngaged), RpcTarget.Others, value);
    }

    private void ApplyEngaged(bool value)
    {
        if (GetData() is not BossMonster data) return;
        if (data == null) return;

        bossEngaged = value;
        if (value) BossEvents.Engage(data, data.Name, transform);
        else BossEvents.Disengage(data);
    }

    [PunRPC]
    protected void RpcBossEngaged(bool value)
    {
        if (bossEngaged == value) return;
        ApplyEngaged(value);
    }
    #endregion
}
