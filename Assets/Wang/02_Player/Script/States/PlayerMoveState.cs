using UnityEngine;
using static InputManager;

public sealed class PlayerMoveState : PlayerState
{
    private static readonly int walkHash = Animator.StringToHash("Walk");
    private readonly float moveSpeed;
    private readonly float directionTurnSpeed;
    private Vector2 moveInput;
    private Vector2 moveDir;
    public override Vector2 MoveDirection => moveDir;
    public override Vector3 Velocity => owner.ViewRotation * new Vector3(moveDir.x, 0f, moveDir.y)
        * (moveSpeed * moveInput.magnitude);

    public PlayerMoveState(FSM owner, SOState data, float moveSpeed, float directionTurnSpeed)
        : base(owner, data)
    {
        this.moveSpeed = moveSpeed;
        this.directionTurnSpeed = directionTurnSpeed;
    }

    public override bool CanEnter() => SelectState(owner) == Type;

    public override void Enter()
    {
        base.Enter();
        owner.Animator.SetBool(walkHash, true);
        moveDir = owner.PreviousState.MoveDirection;

        UpdateMovement();
    }

    public override void UpdateState()
    {
        FSM.StateType nextType = SelectState(owner);

        if (nextType != Type)
        {
            owner.ChangeState(nextType);
            return;
        }

        UpdateMovement();
    }

    public override void FixedUpdateState()
    {
        owner.AddVel(Velocity);
    }

    private void UpdateMovement()
    {
        moveInput = Vector2.ClampMagnitude(Instance.InputInfo.MoveDir, 1f);

        if (moveInput.sqrMagnitude <= 0.001f)
        {
            moveDir = Vector2.zero;
            return;
        }

        float targetAngle = Mathf.Atan2(moveInput.x, moveInput.y) * Mathf.Rad2Deg;
        float angle = targetAngle;
        if (moveDir.sqrMagnitude > 0f)
        {
            float currentAngle = Mathf.Atan2(moveDir.x, moveDir.y) * Mathf.Rad2Deg;

            if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) <= 45f)
            {
                angle = Mathf.MoveTowardsAngle(currentAngle, targetAngle,
                    directionTurnSpeed * Time.deltaTime);
            }
        }

        float radians = angle * Mathf.Deg2Rad;
        moveDir = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    public static FSM.StateType SelectState(FSM owner)
    {
        if (owner.HasGameplayInput == false || Instance.InputInfo.MoveDir.sqrMagnitude <= 0.001f)
            return FSM.StateType.Idle;

        return FSM.StateType.Walk;
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.SetBool(walkHash, false);
    }
}
