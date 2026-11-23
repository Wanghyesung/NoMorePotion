using System.Runtime.InteropServices;
using UnityEngine;

using SF = UnityEngine.SerializeField;

public class UseProjectile : MonoBehaviour
{
    [SF] private MonsterMove mover;
    [SF] private MonsterCombat combat;

    // 투사체 오브젝트
    [SF] private GameObject projectilePrefab;

    // 발사 위치
    [SF] private Transform firePoint;
    // 투사체 속도
    [SF] private float speed = 15f;
    // 최대 비행 거리
    [SF] private float maxDistance = 25f;
    // 타겟 Y축 오프셋
    [SF] private float aimHeight = 1f;

    private void Awake()
    {
        if (mover == null) 
            mover = GetComponent<MonsterMove>();

        if (combat == null) 
            combat = GetComponent<MonsterCombat>();
    }

    /// <summary>
    /// 발사 트리거
    /// </summary>
    public void Fire()
    {
        if (projectilePrefab == null) return;

        Vector3 from = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        Vector3 dir = mover.nowTarget != null
            ? (mover.nowTarget.transform.position + Vector3.up * aimHeight - from).normalized
            : transform.forward;

        GameObject go = PoolManager.Instance.Get(projectilePrefab, from, Quaternion.LookRotation(dir));
        if (go.TryGetComponent(out MonsterProjectileTrigger projectile))
        {
            projectile.Shot(transform, combat.CreateNormalAttackInfo(),
                (IAttacker)combat.GetData(), dir, speed, maxDistance);
        }
    }
}
