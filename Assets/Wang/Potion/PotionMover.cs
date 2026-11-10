using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;

/*///////////////////////////////////////////
                       TestPotion
기능 : 인벤토리와 손에서 사용하는 포션 아이템 컴포넌트
 *///////////////////////////////////////////

[RequireComponent(typeof(ParabolicMovement))]
public class PotionMover : MonoBehaviour
{
    [SerializeField]
    private ParabolicMovement movement;

    [SerializeField]
    private Potion potion;

    public PotionData Data => potion.Data;
    public PotionInstance PotionInstance => potion.Instance;
    public bool HasExploded { get; private set; }
    private bool canExplodeOnContact;

    [SerializeField] private LayerMask mask;
    [SerializeField] private GameObject impactPrefab;
    [SerializeField] private Renderer potionRenderer;

    [SerializeField, Min(0f)] private float impactLifetime = 5f;


    public void InitializePotionMover()
    {
        if (potion == null)
        {
            Debug.LogError("[TestPotion] : Potion 정보가 들어오지 않았습니다. Critical Error 입니다.");
            Destroy(gameObject);
            return;
        }

        if (potion.Data == null)
        {
            Debug.LogError("[TestPotion] : Potion.Data 정보가 들어오지 않았습니다. Critical Error 입니다.");
            Destroy(gameObject);
            return;
        }

#if UNITY_EDITOR
        if (potionRenderer == null || impactPrefab == null)
        {
            Debug.LogWarning("포션 렌더러 , 파티클이 연결되지 않았습니다. 포션 색상 변경이 적용되지 않습니다.", this);
        }
#endif

        if (movement == null)
        {
            movement = GetComponent<ParabolicMovement>();
        }

        HasExploded = false;
        canExplodeOnContact = false;
        movement.OnHitAction += HandleImpact;
        potionRenderer.material.color = Data != null ? Data.potionColor : Color.white;
    }

    private void OnDisable()
    {
        movement.OnHitAction -= HandleImpact;
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


        if (Data.explosionPrefab != null)
        {
            var explosion = PoolManager.Instance.Get(Data.explosionPrefab, position, Quaternion.identity);
            if (explosion == null)
            {
                Debug.LogError("[Potionover] : 폭발 프리팹이 없습니다");
                Destroy(gameObject);
                return;
            }
            explosion.GetComponent<PotionDetector>()?.Initialize(PotionInstance);

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
            Color color = Data != null ? Data.potionColor : Color.white;
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
