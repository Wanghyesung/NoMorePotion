using UnityEngine;

/*///////////////////////////////////////////
                   PlayerHitState
기능 : 이동 상태를 유지하며 상체 피격 애니메이션 재생
 *///////////////////////////////////////////

public sealed class PlayerHitState : PlayerState
{
    private static readonly int hitHash = Animator.StringToHash("Hit");
    private static readonly int upperBodyAnimationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => upperBodyAnimationSpeedHash;
    private float knockbackPower;
    private Vector3 direction;
    private Vector3 knockbackVelocity;
    private readonly float knockbackDeceleration;
    private readonly Quaternion defaultViewRotation;

    public PlayerHitState(FSM owner, SOState data, float knockbackDeceleration)
        : base(owner, data)
    {
        defaultViewRotation = owner.PlayerView.localRotation;
        this.knockbackDeceleration = knockbackDeceleration;
    }

    public override void Init()
    {
        base.Init();
        animEvent.RegisterFrame(clip, clipEvents[0], FinishHit);
        owner.OnHitAction += OnHit;
    }

    public override void Dispose()
    {
        owner.OnHitAction -= OnHit;
        animEvent.UnregisterFrame(clip, clipEvents[0], FinishHit);
    }

    private void OnHit(CommonStruct.AttackInfo attackInfo)
    {
        // 진행 중인 상체 액션은 피격으로 끊지 않는다.
        if (owner.CurrentActionState != null)
            return;

        knockbackPower = attackInfo.AttackPower;
        direction = attackInfo.Position - owner.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = owner.transform.forward;

        direction.Normalize();
        owner.ResetActionState(this);
    }

    // 일반 입력으로는 진입하지 않고 FSM.TakeDamage에서만 진입
    public override bool CanEnter() => false;

    public override void Enter()
    {
        base.Enter();

        owner.PlayerView.rotation = Quaternion.LookRotation(direction);
       

        knockbackVelocity = -direction * (knockbackPower / owner.Rigidbody.mass);
        owner.Animator.SetTrigger(hitHash);
    }


    public override void FixedUpdateState()
    {
        owner.AddVel(knockbackVelocity);
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDeceleration * Time.fixedDeltaTime);
    }

    private void FinishHit(AnimationEvent animationEvent)
    {
        owner.ClearActionState();
    }

    public override void Exit()
    {
        base.Exit();
        knockbackVelocity = Vector3.zero;
        owner.LockRotate(this, false);
        owner.PlayerView.localRotation = defaultViewRotation;
        owner.Animator.ResetTrigger(hitHash);
    }
}
