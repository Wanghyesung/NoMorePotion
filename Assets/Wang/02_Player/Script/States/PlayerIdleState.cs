public sealed class PlayerIdleState : PlayerState
{
    public PlayerIdleState(FSM owner, SOState data) : base(owner, data) { }

    public override void Enter()
    {
        base.Enter();
        owner.StopHorizontalMovement();
    }
    public override void UpdateState()
    {
        owner.ChangeState(PlayerMoveState.SelectState(owner));
    }
  
}
