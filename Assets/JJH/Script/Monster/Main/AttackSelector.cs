using System;
using System.Collections.Generic;
using UnityEngine;

using Random = UnityEngine.Random;
using SF = UnityEngine.SerializeField;

public enum AttackKind
{
    Normal,
    Skill
}

/// <summary>
/// 공격(또는 스킬) 하나의 선택 조건
/// </summary>
[Serializable]
public class AttackPattern
{
    [Tooltip("Skill이면 Animator Bool 이름 겸 BossSkill.AnimationName")]
    public string Name;
    public AttackKind Kind = AttackKind.Normal;

    [Tooltip("Normal일 때 블렌드 트리 임계값(인덱스)")]
    public int AnimIndex;

    public float MinRange = 0f;
    public float MaxRange = 2f;
    public float Cooldown = 0f;
    public float Weight = 1f;

    [NonSerialized] public float NextUseTime;
    [NonSerialized] private int nameHash;

    public bool IsReady => Time.time >= NextUseTime;
    public bool InRange(float distance) => distance >= MinRange && distance <= MaxRange;
    public int NameHash => nameHash != 0 ? nameHash : (nameHash = Animator.StringToHash(Name));
}

/// <summary>
/// 사거리 안 + 쿨타임 완료된 패턴 중 가중치 추첨
/// 일반 몬스터(쿨타임 0인 공격 여러 개)와 보스(쿨타임 있는 스킬)에 공용
/// </summary>
[Serializable]
public class AttackSelector
{
    [SF] private List<AttackPattern> patterns = new();

    [Tooltip("직전에 쓴 패턴의 가중치 배율 (연속 사용 억제, 1이면 억제 없음)")]
    [SF, Range(0f, 1f)] private float repeatPenalty = 0.3f;

    private readonly List<AttackPattern> candidates = new();
    private AttackPattern last;

    public bool HasPatterns => patterns.Count > 0;
    public IReadOnlyList<AttackPattern> Patterns => patterns;

    /// <summary>
    /// 사거리 내 닿는 상대가 있는지 확인하는 함수
    /// </summary>
    public bool AnyInRange(float distance)
    {
        foreach (AttackPattern p in patterns)
            if (p.InRange(distance)) return true;

        return false;
    }

    /// <summary>
    /// 즉시 사용 가능한 스킬이 있는지 확인하는 함수
    /// </summary>
    public bool AnyReadyInRange(float distance)
    {
        foreach (AttackPattern p in patterns)
            if (p.Weight > 0f && p.IsReady && p.InRange(distance)) return true;

        return false;
    }

    /// <summary>
    /// 패턴 셀렉터
    /// </summary>
    public AttackPattern PatternSelect(float distance)
    {
        candidates.Clear();

        foreach (AttackPattern p in patterns)
            if (p.Weight > 0f && p.IsReady && p.InRange(distance))
                candidates.Add(p);

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        float total = 0f;
        foreach (AttackPattern p in candidates)
            total += GetWeight(p);

        float roll = Random.value * total;
        foreach (AttackPattern p in candidates)
        {
            roll -= GetWeight(p);
            if (roll <= 0f) return p;
        }

        return candidates[^1];
    }

    /// <summary>
    /// 패턴 목록 교체 세팅 함수
    /// </summary>
    public void SetPatterns(IEnumerable<AttackPattern> newPatterns)
    {
        patterns.Clear();
        patterns.AddRange(newPatterns);
        last = null;
    }

    /// <summary>
    /// 사용한 스킬 체크
    /// </summary>
    /// <param name="pattern"></param>
    public void MarkUsed(AttackPattern pattern)
    {
        pattern.NextUseTime = Time.time + pattern.Cooldown;
        last = pattern;
    }

    /// <summary>
    /// 스폰 / 풀 재사용 시 호출
    /// </summary>
    public void ResetCooldowns()
    {
        foreach (AttackPattern p in patterns)
            p.NextUseTime = 0f;

        last = null;
    }

    /// <summary>
    /// 사용 가중치 제어 함수
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    private float GetWeight(AttackPattern p) => 
        p == last ? p.Weight * repeatPenalty : p.Weight;
}
