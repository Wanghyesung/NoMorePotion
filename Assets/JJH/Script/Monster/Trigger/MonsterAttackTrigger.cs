using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;
using static CommonStruct;
using static JJH_Constants;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(Collider))]
public class MonsterAttackTrigger : MonoBehaviour
{
    [SF] protected MonsterCombat combat;

    [Header("부채꼴 공격(선택)")]
    //부채꼴 각도
    [SF, Range(1f, 360f)] private float sectorAngle = 360f;

    private readonly HashSet<IDamageable> hitTargets = new();

    private void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<MonsterCombat>();
    }

    private void OnEnable() 
    {
        hitTargets.Clear();
    }

    private void OnDisable() 
    { 
        hitTargets.Clear(); 
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnTriggerStay(Collider other)
    {
        if (sectorAngle < 360f) TryHit(other);
    }

    /// <summary>
    /// 피격 판정 처리 함수
    /// </summary>
    /// <param name="other"></param>
    private void TryHit(Collider other)
    {
        if (!(other.CompareTag(PLAYER_TAG) || other.CompareTag(CORE_TAG))) return;
        if (!IsInSector(other)) return;
        if (!AttackTarget.IsValid(other.gameObject)) return;
        if (!IsHitTrigger(other.gameObject)) return;
        if (!other.TryGetComponent(out IDamageable target)) return;
        if (!hitTargets.Add(target)) return;

        target.TakeDamage(GetAttackInfo(), (IAttacker)combat.GetData());
        OnHit(other);
    }

    /// <summary>
    /// 트리거 정면 기준 부채꼴 안인지 확인하는 함수
    /// </summary>
    private bool IsInSector(Collider other)
    {
        if (sectorAngle >= 360f) return true;

        Vector3 to = other.ClosestPoint(transform.position) - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return true;   // 꼭짓점에 겹친 경우

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, to) <= sectorAngle * 0.5f;
    }

    /// <summary>
    /// 플레이어는 본인 클라이언트가, 소유자 없는 대상은 방장이 판정
    /// </summary>
    /// <param name="target">판정 대상</param>
    /// <returns>판정할 경우 : true</returns>
    public static bool IsHitTrigger(GameObject target)
    {
        // 오프라인은 전부 판정
        if (!PhotonNetwork.InRoom) 
            return true;   

        if (target.TryGetComponent(out PlayerRegistryMember player) && player.View != null)
            return player.View.IsMine;

        return GameAuthority.HasAuthority;
    }

    protected virtual AttackInfo GetAttackInfo()
    {
        return combat.CreateNormalAttackInfo();
    }

    /// <summary>
    /// 피해를 준 직후 추가 처리 (상태 효과 등)
    /// </summary>
    protected virtual void OnHit(Collider other) { }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (sectorAngle >= 360f || !TryGetComponent(out SphereCollider sphere)) return;

        float radius = sphere.radius * transform.lossyScale.x;
        Vector3 center = transform.TransformPoint(sphere.center);
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Gizmos.color = Color.red;
        const int segments = 24;
        Vector3 prev = center;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -sectorAngle * 0.5f + sectorAngle * i / segments;
            Vector3 point = center + Quaternion.Euler(0f, angle, 0f) * forward * radius;
            Gizmos.DrawLine(prev, point);
            prev = point;
        }
        Gizmos.DrawLine(prev, center);
    }
#endif
}
