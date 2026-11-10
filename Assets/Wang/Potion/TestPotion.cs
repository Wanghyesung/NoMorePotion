using Enzeun.Runtime;
using Enzeun.SO;
using ExitGames.Client.Photon.StructWrapping;
using UnityEngine;

/*///////////////////////////////////////////
                       TestPotion
기능 : 인벤토리와 손에서 사용하는 포션 아이템 컴포넌트
 *///////////////////////////////////////////

[RequireComponent(typeof(ParabolicMovement))]
public class TestPotion : MonoBehaviour
{
    private ParabolicMovement movement;
    [SerializeField] private PotionData data;
    public PotionData Data => data;
    public PotionInstance Instance { get; private set; }
    public bool HasExploded { get; private set; }
    private bool canExplodeOnContact;
    [SerializeField] private LayerMask mask;
    [SerializeField] private PotionDetector attackObjectPrefab;   //실제 범위 탐지 주체
    [SerializeField] private GameObject impactPrefab;
    [SerializeField] private Renderer potionRenderer;

    [SerializeField, Min(0f)] private float impactLifetime = 5f;
    private void Awake()
    {
        if(potionRenderer == null || impactPrefab == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("포션 렌더러 , 파티클이 연결되지 않았습니다. 포션 색상 변경이 적용되지 않습니다.", this);
#endif
        }
        movement = GetComponent<ParabolicMovement>();
        // 기존 프리팹의 Potion에 연결된 데이터를 유지한다.
        if (data == null && TryGetComponent(out Enzeun.Runtime.Potion legacyPotion) == true)
            data = legacyPotion.Data;
    }

    private void OnEnable()
    {
        movement.OnHitAction += HandleImpact;
        potionRenderer.material.color = data != null ? data.potionColor : Color.white;
    }
    private void OnDisable()
    {
        movement.OnHitAction -= HandleImpact;
    }

    public void Initialize(PotionData potionData)
    {
        Initialize(new PotionInstance(potionData));
    }

    public void Initialize(PotionInstance instance)
    {
        Instance = instance;
        data = instance != null ? instance.Data : null;
        HasExploded = false;
        canExplodeOnContact = false;
    }

    // 손에 보관하는 동안에는 비행과 충돌 검사를 실행하지 않는다.
    public void Hold(Transform socket)
    {
        canExplodeOnContact = false;
        movement.AttachTo(socket);
    }

    public void Throw(ThrowData data)
    {
        movement.enabled = true;
        movement.Launch(data, mask);
        canExplodeOnContact = true;
    }

    public void Drink(Vector3 position)
    {
        canExplodeOnContact = false;
        ExplodeAt(position);
    }

    public void ExplodeAt(Vector3 position)
    {
        if (HasExploded == true)
            return;

        HasExploded = true;
        canExplodeOnContact = false;
        movement.enabled = false;


        if (data.explosionPrefab != null)
        {
            GameObject explosion = PoolManager.Instance.Get(data.explosionPrefab, position, Quaternion.identity);
            explosion.GetComponent<PotionDetector>()?.Initialize(Instance ?? new PotionInstance(data));

        }
        //Tem
        else if (attackObjectPrefab != null)
        {
            PotionDetector detector = Instantiate(attackObjectPrefab, position, Quaternion.identity);
            detector.Initialize(Instance ?? new PotionInstance(data));
        }
      
        Destroy(gameObject);
    }

    // hitPoint: SphereCast로 얻은 월드 충돌 위치
    private void HandleImpact(Vector3 hitPoint)
    {
        if (canExplodeOnContact == false || HasExploded == true)
            return;

        canExplodeOnContact = false;
        movement.enabled = false;

        if (impactPrefab != null)
        {
            GameObject particleObj =
                PoolManager.Instance.Get(impactPrefab, hitPoint, Quaternion.identity);

            var particle = particleObj.GetComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Color color = data != null ? data.potionColor : Color.white;
            var main = particle.main;
            main.startColor = color;

            particle.Play(true);
            TimeManager.Instance.Schedule(1.0f, () =>
            {
                PoolManager.Instance.Release(particleObj);
            });
        }

        ExplodeAt(hitPoint);
    }
}
