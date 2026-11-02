using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

public sealed class PlayerRollState : PlayerState
{
    private static readonly int rollHash = Animator.StringToHash("Roll");
    private readonly float rollPower;
    private readonly float rollDeceleration;
    private Vector3 direction;
    private InputAction rollAction;

    public override Vector2 MoveDirection
    {
        get
        {
            Vector3 localDirection = Quaternion.Inverse(owner.ViewRotation) * direction;
            return new Vector2(localDirection.x, localDirection.z);
        }
    }

    public PlayerRollState(FSM owner, SOState data, float rollPower, float rollDeceleration)
        : base(owner, data)
    {
        this.rollPower = rollPower;
        this.rollDeceleration = rollDeceleration;
    }

    public override void Init()
    {
        rollAction = Instance.RollAction;
        rollAction.performed += requestRoll;
    }

    public override void Dispose()
    {
        rollAction.performed -= requestRoll;
    }

    private void requestRoll(InputAction.CallbackContext context)
    {
        owner.ChangeState(FSM.StateType.Roll);
    }

    public override bool CanEnter()
    {
        return owner.CurrentState.Type != FSM.StateType.Jump
            && owner.IsGrounded;
    }

    public override void Enter()
    {
        base.Enter();
        direction = owner.transform.forward;

        owner.Animator.SetTrigger(rollHash);

        owner.StopHorizontalMovement();
        owner.Rigidbody.AddForce(direction * rollPower, ForceMode.Impulse);
    }

    public override void FixedUpdateState()
    {
        Vector3 velocity = owner.Rigidbody.linearVelocity;

        float vertical = velocity.y;
        velocity.y = 0f;
        velocity = Vector3.MoveTowards(velocity, Vector3.zero, rollDeceleration * Time.fixedDeltaTime);
        velocity.y = vertical;

        owner.Rigidbody.linearVelocity = velocity;

    }

    public void FinishRoll() => owner.ChangeState(FSM.StateType.Idle);

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(rollHash);
    }
}
