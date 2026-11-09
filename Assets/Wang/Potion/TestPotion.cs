using UnityEngine;
using Enzeun.Runtime;
using Enzeun.SO;

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
    [SerializeField] private PotionDetector attackObjectPrefab;
    [SerializeField] private GameObject impactPrefab;
    [SerializeField, Min(0f)] private float impactLifetime = 5f;
    private void Awake()
    {
        movement = GetComponent<ParabolicMovement>();
        // 기존 프리팹의 Potion에 연결된 데이터를 유지한다.
        if (data == null && TryGetComponent(out Enzeun.Runtime.Potion legacyPotion) == true)
            data = legacyPotion.Data;
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

    // 손에 보관하는 동안에는 Trigger에 닿아도 폭발하지 않는다.
    public void Hold(Transform owner)
    {
        canExplodeOnContact = false;
        movement.enabled = false;
    }

    public void Throw(ThrowData data)
    {
        movement.enabled = true;
        movement.Launch(data);
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
        
        //지금은 직렬화 했지만 SO에 넣으면 좋겠습니다.
        if (attackObjectPrefab != null)
        {
            PotionDetector detector = Instantiate(attackObjectPrefab, position, Quaternion.identity);
            detector.Initialize(Instance ?? new PotionInstance(data));
        }
      
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (canExplodeOnContact == false || HasExploded == true)
            return;
       
        int otherLayer = (1 << other.gameObject.layer);

        if((otherLayer & mask) != 0)
        {
            Vector3 delta = transform.position - movement.PrePos;
            float distance = delta.magnitude;
            Vector3 hitPoint;

            if (distance > 0.0001f && other.Raycast(new Ray(movement.PrePos, delta / distance), out RaycastHit hit, distance))
                hitPoint = hit.point;
            else
                hitPoint = other.ClosestPoint(transform.position);

            canExplodeOnContact = false;
            movement.enabled = false;
            if (impactPrefab != null)
            {
                GameObject impact = Instantiate(impactPrefab, hitPoint, Quaternion.identity);
                Destroy(impact, impactLifetime);
            }
            ExplodeAt(hitPoint);
        }
    }

}
