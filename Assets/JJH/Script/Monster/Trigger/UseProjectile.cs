using System.Runtime.InteropServices;
using UnityEngine;
using static CommonStruct;
using SF = UnityEngine.SerializeField;

public class UseProjectile : MonoBehaviour
{
    [Header("공통")]
    [SF] private MonsterMove mover;
    [SF] private MonsterCombat combat;

    // 투사체 오브젝트
    [SF] private GameObject projectilePrefab;

    [Header("일반 투사체")]
    // 발사 위치
    [SF] private Transform firePoint;
    // 투사체 속도
    [SF] private float speed = 15f;
    // 최대 비행 거리
    [SF] private float maxDistance = 25f;
    // 타겟 Y축 오프셋
    [SF] private float aimHeight = 1f;

    [Header("방사형 투사체")]
    [SF] private GameObject spreadProjectilePrefab;
    [SF] private int count = 5;
    [Tooltip("전체 벌어지는 각도 (360이면 사방으로 고르게)")]
    [SF] private float spreadAngle = 90f;
    [SF] private float spreadSpeed = 8f;
    [SF] private float spreadMaxDistance = 20f;
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

        GameObject obj = 
            PoolManager.Instance.Get(projectilePrefab, from, Quaternion.LookRotation(dir));
        if (obj.TryGetComponent(out MonsterProjectileTrigger projectile))
        {
            projectile.Shoot(transform, combat.CreateNormalAttackInfo(),
                combat.GetData(), dir, speed, maxDistance);
        }
    }

    public void SpreadFire(int skillNum)
    {
        if (projectilePrefab == null || combat.IsSkillCanceled) return;

        Vector3 from = firePoint != null ? firePoint.position : transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        AttackInfo info = combat.CreateSkillAttackInfo(skillNum);
        IAttacker attacker = combat.GetData();

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, GetAngle(i), 0f) * forward;
            GameObject go = PoolManager.Instance.Get(spreadProjectilePrefab, from, Quaternion.LookRotation(dir));

            if (go.TryGetComponent(out MonsterProjectileTrigger projectile))
                projectile.Shoot(transform, info, attacker, dir, spreadSpeed, spreadMaxDistance);
        }
    }

    /// <summary>
    /// i번째 갈래의 각도 (정면 0 기준 좌우 대칭)
    /// </summary>
    private float GetAngle(int i)
    {
        if (count <= 1) 
            return 0f;

        if (spreadAngle >= 360f) 
            return 360f / count * i;

        return Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, (float)i / (count - 1));
    }
}
