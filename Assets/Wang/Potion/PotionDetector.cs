using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;

// 범위 진입과 반복 공격 시점만 관리한다. 실제 대상 탐색과 효과는 PotionExplosion에 맡긴다.
[RequireComponent(typeof(SphereCollider))]
public sealed class PotionDetector : MonoBehaviour
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
        // 로컬 반지름 1 × 루트 스케일 = Explode가 검사하는 월드 반지름.
        float radius = config.GetRadius(instance.Data.potionExplosionRange);
        transform.localScale = Vector3.one * radius;
        targetLayers = config.TargetLayers;

        attackCollider.enabled = true;

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
}
