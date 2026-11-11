using System.Collections.Generic;
using UnityEngine;

using SF = UnityEngine.SerializeField;

public class AttackTarget : MonoBehaviour
{
    [Header("대상 정보")]
    // 물체의 반지름
    [SF] private float bodyRadius = 0.5f;

    // 웨이브 스폰의 경우 활성화
    [SF] private bool isObjective;

    [Header("공격 슬롯")]
    // 최대 공격 가능 슬롯
    [SF] private int meleeSlots = 3;
    // 슬롯 대기 타임아웃
    [SF] private float slotTimeout = 1.5f;

    private readonly Dictionary<MonsterMove, float> holders = new();
    private readonly List<MonsterMove> expired = new();

    private bool isTargetable = true;
    public bool IsTargetable => isTargetable && isActiveAndEnabled;

    public float BodyRadius => bodyRadius;
    public bool IsObjective => isObjective;

    /// <summary>
    /// 슬롯 요청 함수
    /// </summary>
    public bool TryAcquire(MonsterMove mover)
    {
        if (holders.ContainsKey(mover))
        {
            holders[mover] = Time.time;
            return true;
        }

        CleanUp();

        if (holders.Count >= meleeSlots) return false;

        holders[mover] = Time.time;
        return true;
    }

    /// <summary>
    /// 획득하지 않고 지금 들어갈 자리가 있는지만 확인 (대기 판정용)
    /// </summary>
    public bool CanAcquire(MonsterMove mover)
    {
        if (holders.ContainsKey(mover)) return true;

        CleanUp();
        return holders.Count < meleeSlots;
    }

    public void Release(MonsterMove mover) => holders.Remove(mover);

    /// <summary>
    /// 죽었거나, 꺼졌거나, 타겟을 바꿨거나, 오래 갱신 없는 몬스터의 슬롯 회수
    /// </summary>
    private void CleanUp()
    {
        expired.Clear();
        float now = Time.time;

        foreach (KeyValuePair<MonsterMove, float> data in holders)
        {
            MonsterMove mover = data.Key;
            bool invalid = mover == null || 
                !mover.isActiveAndEnabled || 
                mover.nowTarget != gameObject || 
                now - data.Value > slotTimeout;

            if (invalid) expired.Add(mover);
        }

        foreach (MonsterMove mover in expired)
            holders.Remove(mover);
    }

    /// <summary>
    /// 플레이어 사망 시 false, 부활 시 true로 호출
    /// </summary>
    public void SetTargetable(bool value)
    {
        isTargetable = value;
        if (!value) holders.Clear();   // 공격 중이던 몬스터들의 슬롯 즉시 회수
    }

    /// <summary>
    /// 공격 대상으로 유효한지 (파괴, 비활성, 사망 모두 걸러냄)
    /// </summary>
    public static bool IsValid(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        return !target.TryGetComponent(out AttackTarget t) || t.IsTargetable;
    }

    private void OnEnable()
    { 
        // 다시 켜지면(부활, 풀 재사용) 공격 가능
        isTargetable = true; 
    }   

    // 대상이 사라지면(플레이어 사망 등) 전부 비움
    private void OnDisable() 
    { 
        holders.Clear(); 
    }
}
