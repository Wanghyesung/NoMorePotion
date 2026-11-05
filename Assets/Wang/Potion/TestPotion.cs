using UnityEngine;

/*///////////////////////////////////////////
                       TestPotion
기능 : 인벤토리와 손에서 사용하는 포션 아이템 컴포넌트
 *///////////////////////////////////////////

[RequireComponent(typeof(ParabolicMovement))]
public class TestPotion : MonoBehaviour
{
    private ParabolicMovement movement;
    [SerializeField] private LayerMask mask;
    private void Awake()
    {
        movement = GetComponent<ParabolicMovement>();
    }


    private void OnTriggerEnter(Collider other)
    {
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

            other.GetComponent<IDamageable>()?.TakeDamage(new CommonStruct.AttackInfo
            {
                Damage = 10,
                AttackPower = 1,
                Position = hitPoint,
                //Debug.Log("TestPotion hit: " + other.name + " at " + hitPoint.ToString());
            });
            //Debug.Log("Po
        }
    }

}
