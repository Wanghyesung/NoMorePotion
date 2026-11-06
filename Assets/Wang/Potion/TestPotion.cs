using UnityEngine;
using Enzeun.Runtime;

/*///////////////////////////////////////////
                       TestPotion
기능 : 인벤토리와 손에서 사용하는 포션 아이템 컴포넌트
 *///////////////////////////////////////////

[RequireComponent(typeof(ParabolicMovement))]
[RequireComponent(typeof(Enzeun.Runtime.Potion))]
public class TestPotion : MonoBehaviour
{
    private ParabolicMovement movement;
    private Enzeun.Runtime.Potion potion;
    private bool canExplodeOnContact;
    [SerializeField] private LayerMask mask;
    private void Awake()
    {
        movement = GetComponent<ParabolicMovement>();
        potion = GetComponent<Enzeun.Runtime.Potion>();
    }

    public void Initialize(PotionInstance instance)
    {
        potion.Initialize(instance);
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
        potion.ExplodeAt(position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canExplodeOnContact || potion.HasExploded)
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
            potion.ExplodeAt(hitPoint);
        }
    }

}
