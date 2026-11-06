using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;

// 범위 진입과 반복 공격 시점만 관리한다. 실제 대상 탐색과 효과는 PotionExplosion에 맡긴다.
[RequireComponent(typeof(SphereCollider))]
public sealed class AttackObject : MonoBehaviour
{
    [SerializeField] private bool attackOnce = true;
    [SerializeField, Min(0.02f)] private float lifetime = 5f; //여기 잡 큐로 바꾸기

    private SphereCollider attackCollider;
    private PotionInstance instance;
    private LayerMask targetLayers;
    private float nextAttackTime;
    private bool hasAttacked;

    private void Awake()
    {
        attackCollider = GetComponent<SphereCollider>();
        attackCollider.isTrigger = true;
        attackCollider.enabled = false;
    }

    public void Initialize(PotionInstance potionInstance)
    {
        PotionExplosionConfig config = PotionExplosionConfig.Instance;
        if (potionInstance == null || potionInstance.Data == null || config == null)
        {
            Debug.LogError("AttackObject에 포션 데이터와 폭발 범위 설정이 필요합니다.", this);
            Destroy(gameObject);
            return;
        }

        instance = potionInstance;
        // 루트 스케일은 1로 유지해 Explode의 월드 반지름과 일치시킨다.
        transform.localScale = Vector3.one;
        attackCollider.center = Vector3.zero;
        attackCollider.radius = config.GetRadius(instance.Data.potionExplosionRange);
        targetLayers = config.TargetLayers;

        attackCollider.enabled = true;
        Destroy(gameObject, lifetime);
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

        nextAttackTime = Time.time + 1f;
        hasAttacked = true;
        if (attackOnce == true)
            attackCollider.enabled = false;

        PotionExplosion.Explode(instance, transform.position);
    }
}
