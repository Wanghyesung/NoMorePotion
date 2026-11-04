using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
                   PlayerTakeState
기능 : F 입력으로 꺼내기 애니메이션을 재생하고 이벤트 시점의 손에 포션 연결
 *///////////////////////////////////////////

public sealed class PlayerTakeState : PlayerState
{
    #region AnimationParameters
    private static readonly int takeLeftHash = Animator.StringToHash("TakeLeft");
    private static readonly int takeRightHash = Animator.StringToHash("TakeRight");
    private static readonly int animationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => animationSpeedHash;
    #endregion

    #region Runtime
    private readonly Hand hand;
    private readonly AnimationClip rightClip;
    private AnimationEvent[] rightClipEvents;
    private bool hasTaken;
    #endregion

    public PlayerTakeState(FSM owner, SOTakeState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        rightClip = data.RightClip;
    }

    public override void Init()
    {
        base.Init();
        rightClipEvents = rightClip.events;
        animEvent.RegisterFrame(clip, clipEvents[0], TakePotion);
        animEvent.RegisterFrame(clip, clipEvents[1], FinishTake);
        animEvent.RegisterFrame(rightClip, rightClipEvents[0], TakePotion);
        animEvent.RegisterFrame(rightClip, rightClipEvents[1], FinishTake);
        InputManager.Instance.TakeOutPotionAction.performed += RequestTake;
    }

    public override void Dispose()
    {

        animEvent.UnregisterFrame(clip, clipEvents[0], TakePotion);
        animEvent.UnregisterFrame(clip, clipEvents[1], FinishTake);
        animEvent.UnregisterFrame(rightClip, rightClipEvents[0], TakePotion);
        animEvent.UnregisterFrame(rightClip, rightClipEvents[1], FinishTake);
        if (InputManager.Instance != null)
            InputManager.Instance.TakeOutPotionAction.performed -= RequestTake;
    }

    private void RequestTake(InputAction.CallbackContext context)
        => owner.ChangeActionState(FSM.StateType.Take);

    public override bool CanEnter()
    {
        if (owner.CurrentActionState != null)
            return false;

        if (owner.CurrentState.Type != FSM.StateType.Idle && owner.CurrentState.Type != FSM.StateType.Walk)
            return false;

        return hand.FocusedPotion == null || hand.OtherPotion == null;
    }

    public override void Enter()
    {
        base.Enter();
        hasTaken = false;
        owner.Animator.SetTrigger(hand.TakeSide == Hand.Side.Left ? takeLeftHash : takeRightHash);
    }

    private void TakePotion(AnimationEvent animationEvent)
    {
        if (hasTaken == true)
            return;

        hasTaken = true;
        hand.TakeOutPotion();
    }

    private void FinishTake(AnimationEvent animationEvent) => owner.ClearActionState();

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(takeLeftHash);
        owner.Animator.ResetTrigger(takeRightHash);
    }
}
