using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

public sealed class PlayerJumpState : PlayerState
{
    private static readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly float jumpSpeed;
    private Vector3 airVelocity;
    private bool applyJump;
    public override Vector3 Velocity => airVelocity;
   
    public PlayerJumpState(FSM owner, SOState data, float jumpSpeed)
        : base(owner, data)
    {
        this.jumpSpeed = jumpSpeed;
    }

    public override void Init()
    {
        base.Init();
        animEvent.RegisterFrame(clip, clipEvents[0], HoldPose);
        Instance.JumpAction.performed += RequestJump;
    }

    public override void Dispose()
    {
        animEvent.UnregisterFrame(clip, clipEvents[0], HoldPose);
        if(Instance != null)
            Instance.JumpAction.performed -= RequestJump;
    }

    private void RequestJump(InputAction.CallbackContext context) => owner.ChangeState(FSM.StateType.Jump);

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

        airVelocity = owner.PreviousState.Velocity;
        applyJump = true;

        owner.ClearGroundContact();
    }

    private void HoldPose(AnimationEvent animationEvent)
    {
        if (owner.IsGrounded == false)
            owner.Animator.SetFloat(AnimationSpeedHash, 0f);
    }

    public override void FixedUpdateState()
    {
        owner.AddVel(airVelocity);
        if (applyJump)
        {
            owner.AddVel(Vector3.up * (jumpSpeed - owner.Rigidbody.linearVelocity.y));
            applyJump = false;
        }
    }

    public override void UpdateState()
    {
        if (owner.IsGrounded == true)
            owner.ChangeState(FSM.StateType.Idle);
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(jumpHash);
    }
}
