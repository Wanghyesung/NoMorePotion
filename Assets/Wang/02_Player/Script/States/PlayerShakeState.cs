using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
                   PlayerShakeState
기능 : R 입력으로 흔들기 모션을 재생하고 0번 이벤트에서 흔들기, 1번에서 종료
 *///////////////////////////////////////////

public sealed class PlayerShakeState : PlayerState
{
    private static readonly int shakeLeftHash = Animator.StringToHash("ShakeLeft");
    private static readonly int shakeRightHash = Animator.StringToHash("ShakeRight");
    private static readonly int shakeBothHash = Animator.StringToHash("ShakeBoth");
    private static readonly int animationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => animationSpeedHash;

    private readonly Hand hand;
    private readonly AnimationClip rightClip;
    private readonly AnimationClip bothClip;
    private AnimationEvent[] rightEvents;
    private AnimationEvent[] bothEvents;

    public PlayerShakeState(FSM owner, SOShakeState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        rightClip = data.RightClip;
        bothClip = data.BothClip;
    }

    public override void Init()
    {
        base.Init();
        rightEvents = rightClip.events;
        bothEvents = bothClip.events;
        animEvent.RegisterFrame(clip, clipEvents[0], ExecuteShake);
        animEvent.RegisterFrame(clip, clipEvents[1], FinishShake);
        animEvent.RegisterFrame(rightClip, rightEvents[0], ExecuteShake);
        animEvent.RegisterFrame(rightClip, rightEvents[1], FinishShake);
        animEvent.RegisterFrame(bothClip, bothEvents[0], ExecuteShake);
        animEvent.RegisterFrame(bothClip, bothEvents[1], FinishShake);
        InputManager.Instance.ShakeAction.performed += RequestShake;
    }

    public override void Dispose()
    {
        animEvent.UnregisterFrame(clip, clipEvents[0], ExecuteShake);
        animEvent.UnregisterFrame(clip, clipEvents[1], FinishShake);
        animEvent.UnregisterFrame(rightClip, rightEvents[0], ExecuteShake);
        animEvent.UnregisterFrame(rightClip, rightEvents[1], FinishShake);
        animEvent.UnregisterFrame(bothClip, bothEvents[0], ExecuteShake);
        animEvent.UnregisterFrame(bothClip, bothEvents[1], FinishShake);
        if (InputManager.Instance != null)
            InputManager.Instance.ShakeAction.performed -= RequestShake;
    }

    private void RequestShake(InputAction.CallbackContext context)
    {
        if (owner.HasGameplayInput == true)
            owner.ChangeActionState(FSM.StateType.Shake);
    }

    public override bool CanEnter()
    {
        if (owner.CurrentState == null || owner.CurrentActionState != null)
            return false;
        if (owner.CurrentState.Type != FSM.StateType.Idle && owner.CurrentState.Type != FSM.StateType.Walk)
            return false;
        return hand.IsBothHandsEmpty == false;
    }

    public override void Enter()
    {
        base.Enter();
        bool hasLeft = hand.GetPotion(Hand.Side.Left) != null;
        bool hasRight = hand.GetPotion(Hand.Side.Right) != null;
        owner.Animator.SetTrigger(hasLeft == true && hasRight == true ? shakeBothHash
            : hasLeft == true ? shakeLeftHash : shakeRightHash);
    }

    private void ExecuteShake(AnimationEvent animationEvent)
    {
        //hand.Shake();
    }
    private void FinishShake(AnimationEvent animationEvent)
    {
        if (owner.CurrentActionState == this)
            owner.ClearActionState();
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(shakeLeftHash);
        owner.Animator.ResetTrigger(shakeRightHash);
        owner.Animator.ResetTrigger(shakeBothHash);
    }
}
