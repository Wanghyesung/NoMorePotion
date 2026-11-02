using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

public sealed class PlayerJumpState : PlayerState
{
    private static readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly float jumpSpeed;
    private InputAction jumpAction;

    public PlayerJumpState(FSM owner, SOState data, float jumpSpeed)
        : base(owner, data)
    {
        this.jumpSpeed = jumpSpeed;
    }

    public override void Init()
    {
        jumpAction = Instance.JumpAction;
        jumpAction.performed += requestJump;
    }

    public override void Dispose() => jumpAction.performed -= requestJump;

    private void requestJump(InputAction.CallbackContext context)
        => owner.ChangeState(FSM.StateType.Jump);

    public override bool CanEnter()
    {
        if (owner.CurrentState.Type != FSM.StateType.Idle &&
            owner.CurrentState.Type != FSM.StateType.Walk)
            return false;

        if (owner.IsGrounded == false)
            return false;

        return true;
    }

    public override void Enter()
    {
        base.Enter();
        owner.Animator.SetTrigger(jumpHash);

        Vector3 velocity = owner.Rigidbody.linearVelocity;
        velocity.y = jumpSpeed;
        owner.Rigidbody.linearVelocity = velocity;

        owner.ClearGroundContact();
    }

    public void HoldPose()
    {
        if (!owner.IsGrounded)
            owner.Animator.speed = 0f;
    }

    public override void UpdateState()
    {
        if (owner.IsGrounded)
            owner.ChangeState(FSM.StateType.Idle);
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(jumpHash);
    }
}
