public sealed class PlayerDeadState : PlayerState
{
    private readonly PlayerRagdoll ragdoll;
    private CommonStruct.AttackInfo attackInfo;

    public PlayerDeadState(FSM owner, SOState data) : base(owner, data)
    {
        ragdoll = owner.GetComponent<PlayerRagdoll>();
    }

    public void SetAttackInfo(CommonStruct.AttackInfo info) => attackInfo = info;
    public override bool CanEnter() => false;

    public override void Enter()
    {
        owner.LockState(true);
        owner.LockRotate(this, true);
        owner.ClearGroundContact();
        ragdoll.Play(attackInfo.Position);
    }

    public override void Exit()
    {
        owner.LockState(false);
        owner.LockRotate(this, false);
    }
}
