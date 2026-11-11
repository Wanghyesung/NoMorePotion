using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using SF = UnityEngine.SerializeField;

public class AttackTarget : MonoBehaviour
{
    private class Holder
    {
        public float LastTime;   // 마지막 갱신 시각
        public int Index;        // 배정된 각도 슬롯 번호
    }

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

    private readonly Dictionary<MonsterMove, Holder> holders = new();
    private readonly List<MonsterMove> expired = new();
    private readonly bool[] usedIndex = new bool[32];

    private bool isTargetable = true;
    public bool IsTargetable => isTargetable && isActiveAndEnabled;

    public float BodyRadius => bodyRadius;
    public bool IsObjective => isObjective;
    private int SlotCount => Mathf.Clamp(meleeSlots, 1, usedIndex.Length);


    /// <summary>
    /// 슬롯 요청 함수
    /// </summary>
    public bool TryAcquire(MonsterMove mover)
    {
        if (holders.TryGetValue(mover, out Holder holder))
        {
            holder.LastTime = Time.time;
            return true;
        }

        CleanUp();

        if (holders.Count >= SlotCount) return false;

        // 다가오는 방향과 가장 가까운 빈 자리 배정
        holders[mover] = new Holder
        {
            LastTime = Time.time,
            Index = PickFreeIndex(mover.transform.position)
        };
        return true;
    }

    /// <summary>
    /// 획득하지 않고 지금 들어갈 자리가 있는지만 확인 (대기 판정용)
    /// </summary>
    public bool CanAcquire(MonsterMove mover)
    {
        if (holders.ContainsKey(mover)) return true;

        CleanUp();
        return holders.Count < SlotCount; 
    }
    // 현재 배치중인 객체 제거
    public void Release(MonsterMove mover) => holders.Remove(mover);
    // 현재 배치중인 객체 확인
    public bool Holds(MonsterMove mover) => holders.ContainsKey(mover);

    /// <summary>
    /// 배정된 공격 위치
    /// </summary>
    public Vector3 GetSlotPoint(MonsterMove mover, float standDistance)
    {
        if (!holders.TryGetValue(mover, out Holder h))
            return transform.position;

        Vector3 point = 
            transform.position + IndexToDirection(h.Index) * (bodyRadius + standDistance);

        return NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position : point;
    }

    

    /// <summary>
    /// 월드 기준 해당 인덱스의 고정 각도 위치 출력
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    private Vector3 IndexToDirection(int index)
    {
        float angle = index * 360f / SlotCount;
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }

    /// <summary>
    /// 가장 가까운 빈자리를 찾아 지정하는 함수
    /// </summary>
    /// <param name="fromPosition">현재 위치</param>
    /// <returns>슬롯 Index(int)</returns>
    private int PickFreeIndex(Vector3 fromPosition)
    {
        for (int i = 0; i < SlotCount; i++) usedIndex[i] = false;
        foreach (Holder h in holders.Values) usedIndex[h.Index] = true;

        Vector3 dir = fromPosition - transform.position;
        dir.y = 0f;

        int best = 0;
        float bestAngle = float.MaxValue;

        for (int i = 0; i < SlotCount; i++)
        {
            if (usedIndex[i]) continue;
            // 방향을 알 수 없으면 앞 번호부터
            float angle = 
                dir.sqrMagnitude > 0.001f ? Vector3.Angle(dir, IndexToDirection(i)) : i;  

            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// 죽었거나, 꺼졌거나, 타겟을 바꿨거나, 오래 갱신 없는 몬스터의 슬롯 회수
    /// </summary>
    private void CleanUp()
    {
        expired.Clear();
        float now = Time.time;

        foreach (KeyValuePair<MonsterMove, Holder> data in holders)
        {
            MonsterMove mover = data.Key;
            bool invalid = mover == null ||
                !mover.isActiveAndEnabled ||
                mover.nowTarget != gameObject ||
                now - data.Value.LastTime > slotTimeout;

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
        if (!value) holders.Clear();
    }

    /// <summary>
    /// 공격 대상으로 유효한지 체크하는 함수
    /// </summary>
    public static bool IsValid(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        return !target.TryGetComponent(out AttackTarget t) || t.IsTargetable;
    }

    private void OnEnable()
    { 
        isTargetable = true; 
    }   

    private void OnDisable() 
    { 
        holders.Clear(); 
    }

    /// <summary>
    /// 테스트용 기즈모
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < SlotCount; i++)
            Gizmos.DrawLine(transform.position,
                transform.position + IndexToDirection(i) * (bodyRadius + 1f));
    }
}
