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

    private readonly RaycastHit[] hits = new RaycastHit[8];

    private Transform owner;
    private AttackInfo info;
    private IAttacker attacker;
    private Vector3 dir;
    private float speed;
    private float remaining;
    private bool alive;

    public void Shot(Transform owner, AttackInfo info, IAttacker attacker,
        Vector3 dir, float speed, float maxDistance)
    {
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

        if (remaining <= 0f)
            Finish(transform.position);
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

        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (owner != null && c.transform.IsChildOf(owner)) continue;   // 자기 몸 무시

            bool isTarget = c.CompareTag(PLAYER_TAG) || c.CompareTag(CORE_TAG);
            bool isObstacle = !c.isTrigger && (obstacleMask.value & (1 << c.gameObject.layer)) != 0;
            if (!isTarget && !isObstacle) continue;
            if (isTarget && !AttackTarget.IsValid(c.gameObject)) continue; // 죽은 대상은 통과

            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                best = i;
                bestIsTarget = isTarget;
            }
        }

        if (best < 0) return false;

        // 시작부터 겹쳐 있으면 distance 0, point가 (0,0,0)으로 나옴
        point = hits[best].distance > 0f ? hits[best].point : transform.position;

        if (bestIsTarget)
            TryDamage(hits[best].collider);

        return true;
    }

    /// <summary>
    /// 타격 가능한 오브젝트와 충돌한 경우 피해
    /// </summary>
    /// <param name="other"></param>
    private void TryDamage(Collider other)
    {
        // 플레이어는 본인 클라이언트, 수정 구슬은 방장만 데미지 적용
        if (!MonsterAttackTrigger.IsHitTrigger(other.gameObject)) return;
        if (other.TryGetComponent(out IDamageable target))
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
