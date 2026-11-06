using UnityEngine;
using UnityEngine.InputSystem;
using static CommonStruct;
public class MobTestPlayer : MonoBehaviour, IDamageable
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 5f;
    private int Hp = 100;
    private Rigidbody rb;
    private InputAction moveAction;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        moveAction = new InputAction("Move", binding: "<Keyboard>/wasd");

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
    }

    private void OnEnable()
    {
        moveAction.Enable();
    }

    private void OnDisable()
    {
        // 스크립트 비활성화 시 입력 액션 중지
        moveAction.Disable();
    }

    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        rb.linearVelocity = new Vector3(moveDirection.x * moveSpeed, rb.linearVelocity.y, moveDirection.z * moveSpeed);

        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 10f);
        }
    }

    /// <summary>
    /// 임시 TakeDamage
    /// </summary>
    /// <param name="attackInfo"></param>
    public void TakeDamage(AttackInfo attackInfo)
    {
        Hp = -attackInfo.Damage;
    }

    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker) 
        => TakeDamage(attackInfo);

    public void TakeDamage(AttackInfo attackInfo, Character attacker) 
        => TakeDamage(attackInfo);
}
