using System.Collections.Generic;
using UnityEngine;
using static CommonStruct;
using static JJH_Constants;

using SF = UnityEngine.SerializeField;
public class MonsterProjectileTrigger : MonoBehaviour
{
    [Tooltip("판정 반지름")]
    [SF] private float radius = 0.3f;
    [Tooltip("닿으면 소멸하는 벽·지형 레이어")]
    [SF] private LayerMask obstacleMask;
    [SF] private GameObject impactEffect;

    [Header("관통형 투사체용")]
    [Tooltip("대상 관통 여부")]
    [SF] private bool pierce;
    [Tooltip("지면 이동 여부")]
    [SF] private bool hugGround;
    [Tooltip("지면 레이어,obstacleMask 중복 x")]
    [SF] private LayerMask groundMask;
    [SF] private float groundOffset = 0f;

    private readonly HashSet<IDamageable> pierced = new();

    private readonly RaycastHit[] hits = new RaycastHit[8];
    private readonly int[] passHits = new int[8];
    private Transform owner;
    private AttackInfo info;
    private IAttacker attacker;
    private Vector3 dir;
    private float speed;
    private float remaining;
    private bool alive;

    public void Shoot(Transform owner, AttackInfo info, IAttacker attacker,
        Vector3 dir, float speed, float maxDistance)
    {
        pierced.Clear();
        if (hugGround)
        {
            dir.y = 0f;
            dir.Normalize();
            this.dir = dir;
        }

        this.owner = owner;
        this.info = info;
        this.attacker = attacker;
        this.dir = dir;
        this.speed = speed;
        remaining = maxDistance;
        alive = true;

        transform.rotation = Quaternion.LookRotation(dir);

        // 풀에서 재사용할 때 이전 위치에서 꼬리가 이어지지 않게
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>())
            trail.Clear();
    }

    private void Update()
    {
        if (!alive) return;

        float step = Mathf.Min(speed * Time.deltaTime, remaining);
        if (CheckHit(step, out Vector3 point))
        {
            Finish(point);
            return;
        }

        transform.position += dir * step;
        remaining -= step;

        if (hugGround) SnapToGround();

        if (remaining <= 0f)
            Finish(transform.position);
    }

    /// <summary>
    /// 지면 추적형 투사체의 경우 지면 추적용 함수
    /// </summary>
    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 p = transform.position;
            p.y = hit.point.y + groundOffset;
            transform.position = p;
        }
    }

    /// <summary>
    /// 이번 프레임 이동 구간에서 가장 가까운 대상이나 벽을 찾음
    /// </summary>
    private bool CheckHit(float step, out Vector3 point)
    {
        point = default;
        int n = Physics.SphereCastNonAlloc(transform.position, radius, dir, hits, step,
            ~0, QueryTriggerInteraction.Collide);

        int best = -1;
        bool bestIsTarget = false;
        float bestDist = float.MaxValue;
        int passCount = 0;

        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (owner != null && c.transform.IsChildOf(owner)) continue;

            bool isTarget = c.CompareTag(PLAYER_TAG) || c.CompareTag(CORE_TAG);
            bool isObstacle = !c.isTrigger && (obstacleMask.value & (1 << c.gameObject.layer)) != 0;
            if (!isTarget && !isObstacle) continue;
            if (isTarget && !AttackTarget.IsValid(c.gameObject)) continue;

            // 관통 대상
            if (isTarget && pierce)
            {
                if (passCount < passHits.Length) passHits[passCount++] = i;
                continue;
            }

            // 피격 후보
            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                best = i;
                bestIsTarget = isTarget;
            }
        }

        for (int k = 0; k < passCount; k++)
        {
            int idx = passHits[k];
            if (hits[idx].distance <= bestDist)
                TryDamage(hits[idx].collider);
        }

        if (best < 0) return false;

        point = hits[best].distance > 0f ? hits[best].point : transform.position;

        if (bestIsTarget)
            TryDamage(hits[best].collider);

        return true;
    }

    /// <summary>
    /// 타격 가능한 오브젝트와 충돌한 경우 피해
    /// </summary>
    /// <param name="other">충돌한 오브젝트</param>
    private void TryDamage(Collider other)
    {
        if (!MonsterAttackTrigger.IsHitTrigger(other.gameObject)) return;
        if (!other.TryGetComponent(out IDamageable target)) return;
        if (pierce && !pierced.Add(target)) return;

        target.TakeDamage(info, attacker);
    }

    /// <summary>
    /// 투사체 이펙트 반환
    /// </summary>
    /// <param name="point"></param>
    private void Finish(Vector3 point)
    {
        alive = false;

        if (impactEffect != null)
            PoolManager.Instance.Get(impactEffect, point, Quaternion.identity);

        PoolManager.Instance.Release(gameObject);
    }
}
