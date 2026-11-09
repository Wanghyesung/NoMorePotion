using UnityEngine;

public sealed class PlayerMoveState : PlayerState
{
    private static readonly int walkHash = Animator.StringToHash("Walk");
    private readonly float moveSpeed;
    private Vector2 moveInput;
    private Vector2 moveDir;
    public override Vector2 MoveDirection => moveDir;
    // 걷기 속도에만 버프 배율을 곱한다. 합산 후 전체 속도에 곱하면 넉백까지 달라진다.
    public override Vector3 Velocity => owner.ViewRotation * new Vector3(moveDir.x, 0f, moveDir.y)
        * (moveSpeed * owner.MoveSpeedMultiplier * moveInput.magnitude);

    public PlayerMoveState(FSM owner, SOState data, float moveSpeed)
        : base(owner, data)
    {
        this.moveSpeed = moveSpeed;
    }

    public override bool CanEnter() => SelectState(owner) == Type;

    public override void Enter()
    {
        base.Enter();
        owner.Animator.SetBool(walkHash, true);

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
        moveInput = Vector2.ClampMagnitude(owner.MoveInput, 1f);

        if (moveInput.sqrMagnitude <= 0.001f)
        {
            moveDir = Vector2.zero;
            return;
        }

        // 이동 기준은 마우스로 회전한 루트다. View 회전은 이동 계산에 다시 섞지 않는다.
        moveDir = moveInput.normalized;
        if (owner.IsRotationLocked == false)
            owner.PlayerView.localRotation = Quaternion.LookRotation(new Vector3(moveDir.x, 0f, moveDir.y));
    }

    public static FSM.StateType SelectState(FSM owner)
    {
        if (owner.HasGameplayInput == false || owner.MoveInput.sqrMagnitude <= 0.001f)
            return FSM.StateType.Idle;

        return FSM.StateType.Walk;
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.SetBool(walkHash, false);
    }
}
