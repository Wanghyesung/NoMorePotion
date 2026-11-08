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
    private Quaternion PreViewRotation;
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

        PreViewRotation = owner.PlayerView.localRotation;
        // 루트/카메라는 돌리지 않고 View만 입력 방향으로 돌린다. 무입력은 현재 View 방향을 유지한다.
        Vector2 input = owner.MoveInput;
        if (input.sqrMagnitude > 0.001f)
        {
            Vector3 inputDirection = owner.transform.rotation * new Vector3(input.x, 0f, input.y);
            owner.PlayerView.rotation = Quaternion.LookRotation(inputDirection);
        }
        direction = owner.PlayerView.forward;

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
        owner.PlayerView.localRotation = PreViewRotation;
        owner.Animator.ResetTrigger(rollHash);
    }
}
