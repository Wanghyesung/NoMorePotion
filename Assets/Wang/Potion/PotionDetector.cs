using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;

// 범위 진입과 반복 공격 시점만 관리한다. 실제 대상 탐색과 효과는 PotionExplosion에 맡긴다.
[RequireComponent(typeof(SphereCollider))]
public sealed class PotionDetector : MonoBehaviour
{
    [SerializeField] private bool attackOnce = true;
    [SerializeField, Min(0.02f)] private float lifetime = 5f; //여기 잡 큐로 바꾸기

    #region 진희진
    [Header("Effect")]
    // 크기를 조절할 이펙트 자식. 비어 있으면 이펙트 크기는 바뀌지 않는다.
    // 이펙트 안의 ParticleSystem은 모두 Scaling Mode = Hierarchy여야 스케일이 적용된다.
    [SerializeField] private Transform effectRoot;
    [SerializeField, Min(0.01f)] private float effectBaseRadius = 1f; // effect scale 1일 때 이펙트 반지름(m)
    #endregion

    private SphereCollider attackCollider;
    private PotionInstance instance;
    private LayerMask targetLayers;
    private float nextAttackTime;
    private bool hasAttacked;

    private void Awake()
    {
        attackCollider = GetComponent<SphereCollider>();
    }

    public void Initialize(PotionInstance potionInstance)
    {
        PotionExplosionConfig config = PotionExplosionConfig.Instance;
        if (potionInstance == null || potionInstance.Data == null || config == null)
        {
            Debug.LogError("PotionDetector에 포션 데이터와 폭발 범위 설정이 필요합니다.", this);
            Destroy(gameObject);
            return;
        }

        instance = potionInstance;

        #region 진희진
        // 판정과 이펙트는 같은 radius(소 3 / 중 5 / 대 8m, PotionExplosionConfig) 따로 적용
        // 루트 스케일은 1로 고정, 루트를 키우면 자식 이펙트까지 같이 커져 크기가 이중으로 곱해진다.

        float radius = config.GetRadius(instance.Data.potionExplosionRange);
        
        transform.localScale = Vector3.one; // 루트는 그대로
        attackCollider.radius = radius; // 트리거 반경 = Explode의 OverlapSphere 반경

        // 이펙트 스케일 = 목표 반지름 / 기준 반지름  (예: 기준 2m, 대 8m → 4배)
        if (effectRoot != null)
            effectRoot.localScale = Vector3.one * (radius / effectBaseRadius);

        // 풀 재사용 대비 초기화
        hasAttacked = false;
        nextAttackTime = 0f;
        targetLayers = config.TargetLayers;
        attackCollider.enabled = true;

        #endregion

        //이거 PoolManager가 먼저 사라지면 터짐
        TimeManager.Instance.Schedule(lifetime, () =>
        {
            PoolManager.Instance.Release(gameObject);
        });
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAttack(other);
    }
    private void OnTriggerStay(Collider other)
    {
        if (attackOnce == true)
            return;

        TryAttack(other);
    }

    private void TryAttack(Collider other)
    {
        if (instance == null)
            return;
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0)
            return;
        if (attackOnce == true && hasAttacked == true)
            return;
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + 1f; // 지속 공격은 콜라이더 개수와 무관하게 범위 전체를 초당 한 번 검사한다.
        hasAttacked = true;
        if (attackOnce == true)
            attackCollider.enabled = false;

        PotionExplosion.Explode(instance, transform.position);
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
