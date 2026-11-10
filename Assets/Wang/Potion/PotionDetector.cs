using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using Cysharp.Threading;
using System;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

// 범위 진입과 반복 공격 시점만 관리한다. 실제 대상 탐색과 효과는 PotionExplosion에 맡긴다.
[RequireComponent(typeof(SphereCollider))]
public sealed class PotionDetector : MonoBehaviour
{
    [SerializeField] private bool attackOnce = true;
    [SerializeField, Min(0.02f)] private float lifetime = 5f;

    #region 진희진
    [Header("Effect")]
    // 크기를 조절할 이펙트 자식. 비어 있으면 이펙트 크기는 바뀌지 않는다.
    // 이펙트 안의 ParticleSystem은 모두 Scaling Mode = Hierarchy여야 스케일이 적용된다.
    [SerializeField] private Transform effectRoot;
    [SerializeField, Min(0.01f)] private float effectBaseRadius = 1f; // effect scale 1일 때 이펙트 반지름(m)
    #endregion

    private PotionInstance instance;
    private float nextAttackDelay = 1f; // 초 단위
    private int AttackCount = 0;
    private int MaxAttackCount = 10;



    public void Initialize(PotionInstance potionInstance)
    {

        if (potionInstance == null || potionInstance.Data == null)
        {
            Debug.LogError("PotionDetector에 포션 데이터와 폭발 범위 설정이 필요합니다.", this);
            Destroy(gameObject);
            return;
        }

        instance = potionInstance;

        #region 진희진
        // 판정과 이펙트는 같은 radius(소 3 / 중 5 / 대 8m, PotionExplosionConfig) 따로 적용
        // 루트 스케일은 1로 고정, 루트를 키우면 자식 이펙트까지 같이 커져 크기가 이중으로 곱해진다.

        float radius = PotionExplosionConfig.Instance.GetRadius(instance.Data.potionExplosionRange);

        transform.localScale = Vector3.one; // 루트는 그대로

        // 이펙트 스케일 = 목표 반지름 / 기준 반지름  (예: 기준 2m, 대 8m → 4배)
        if (effectRoot != null)
            effectRoot.localScale = Vector3.one * (radius / effectBaseRadius);

        #endregion

        if (attackOnce)
            lifetime += MaxAttackCount;

        //이거 PoolManager가 먼저 사라지면 터짐
        TimeManager.Instance.Schedule(lifetime, () =>
        {
            PoolManager.Instance.Release(gameObject);
        });

        TryAttack();
    }

    private void TryAttack()
    {
        if (instance == null)
            return;


        PotionExplosion.Explode(instance, transform.position);

        if (!attackOnce)
        {
            if (AttackCount >= MaxAttackCount)
                return;

            AttackCount += 1;
            WaitNextAttackAsync().Forget();
        }
    }

    private async UniTaskVoid WaitNextAttackAsync()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(nextAttackDelay));

        TryAttack();
    }


#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var c = GetComponent<SphereCollider>();
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, c.radius * transform.lossyScale.x);
    }
#endif
}
