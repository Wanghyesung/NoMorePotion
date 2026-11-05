using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

public sealed class PlayerRollState : PlayerState
{
    private static readonly int rollHash = Animator.StringToHash("Roll");
    private readonly float rollPower;
    private readonly float rollDeceleration;
    private Vector3 direction;
    private Vector3 rollVelocity;
    public override Vector3 Velocity => rollVelocity;
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
        base.Init();
        animEvent.RegisterFrame(clip, clipEvents[0], FinishRoll);
        rollAction = Instance.RollAction;
        rollAction.performed += RequestRoll;
    }

    public override void Dispose()
    {
        animEvent.UnregisterFrame(clip, clipEvents[0], FinishRoll);
        rollAction.performed -= RequestRoll;
    }

    private void RequestRoll(InputAction.CallbackContext context)
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
        owner.LockRotate(this, true);
        direction = owner.transform.forward;

        owner.Animator.SetTrigger(rollHash);

        rollVelocity = direction * (rollPower / owner.Rigidbody.mass);
    }

    public override void FixedUpdateState()
    {
        rollVelocity = Vector3.MoveTowards(
            rollVelocity, Vector3.zero, rollDeceleration * Time.fixedDeltaTime);
        owner.AddVel(rollVelocity);

    }

    private void FinishRoll(AnimationEvent animationEvent) => owner.ChangeState(FSM.StateType.Idle);

    public override void Exit()
    {
        base.Exit();
        owner.LockRotate(this, false);
        owner.Animator.ResetTrigger(rollHash);
    }
}
