using UnityEngine;

/*///////////////////////////////////////////
                   PlayerHitState
기능 : 공격 위치 반대 방향으로 넉백하며 전신 피격 애니메이션 재생
       상태 전환과 회전을 잠그고 종료 이벤트에서 해제
 *///////////////////////////////////////////

public sealed class PlayerHitState : PlayerState
{
    #region Knockback
    private readonly float knockbackDeceleration;
    private float knockbackPower;
    private Vector3 direction;

    #endregion

    private static readonly int hitHash = Animator.StringToHash("Hit");

    public PlayerHitState(FSM owner, SOState data, float knockbackDeceleration)
        : base(owner, data)
    {
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

    // attackInfo.Position: 공격이 들어온 월드 위치
    private void OnHit(CommonStruct.AttackInfo attackInfo)
    {
        direction = owner.transform.position - attackInfo.Position;
        knockbackPower = 1;// attackInfo.AttackPower;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            direction = -owner.transform.forward;

        direction.Normalize();
        owner.ResetState(this);
    }

    // 일반 입력으로는 진입하지 않고 FSM.TakeDamage에서만 진입
    public override bool CanEnter() => false;

    public override void Enter()
    {
        base.Enter();
        owner.LockState(true);
        owner.LockRotate(this, true);

        owner.Rigidbody.linearVelocity = Vector3.zero;
        owner.Rigidbody.AddForce(direction * knockbackPower, ForceMode.Impulse);
        owner.Animator.SetTrigger(hitHash);
    }

    public override void FixedUpdateState()
    {
        Vector3 velocity = owner.Rigidbody.linearVelocity;
        velocity = Vector3.MoveTowards(velocity, Vector3.zero, knockbackDeceleration * Time.fixedDeltaTime);
        owner.Rigidbody.linearVelocity = velocity;
    }

    private void FinishHit(AnimationEvent animationEvent)
    {
        owner.LockState(false);
        owner.ChangeState(FSM.StateType.Idle);
    }

    public override void Exit()
    {
        base.Exit();
        owner.LockState(false);
        owner.LockRotate(this, false);
        owner.Animator.ResetTrigger(hitHash);
        owner.StopHorizontalMovement();
    }
}
