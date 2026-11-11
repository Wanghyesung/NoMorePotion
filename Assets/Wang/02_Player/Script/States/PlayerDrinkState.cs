using UnityEngine;

// 상체 액션 상태. 클립의 첫 이벤트에서 마시고 두 번째 이벤트에서 종료한다.
public sealed class PlayerDrinkState : PlayerState
{
    private static readonly int drinkLeftHash = Animator.StringToHash("DrinkLeft");
    private static readonly int drinkRightHash = Animator.StringToHash("DrinkRight");
    private static readonly int animationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => animationSpeedHash;

    private readonly Hand hand;
    private readonly AnimationClip rightClip;
    private AnimationEvent[] rightClipEvents;
    private Hand.Side drinkingSide;
    private PotionMover drinkingPotion;

    public PlayerDrinkState(FSM owner, SODrinkState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        rightClip = data.RightClip;
    }

    public override void Init()
    {
        base.Init();
        RegisterClipEvents(clip, clipEvents);
        if (rightClip == null)
            return;

        rightClipEvents = rightClip.events;
        RegisterClipEvents(rightClip, rightClipEvents);
    }

    private void RegisterClipEvents(AnimationClip targetClip, AnimationEvent[] events)
    {

        if (events.Length > 0)
            animEvent.RegisterFrame(targetClip, events[0], DrinkPotion);
        if (events.Length > 1)
            animEvent.RegisterFrame(targetClip, events[1], FinishDrink);
    }

    public override void Dispose()
    {
        UnregisterClipEvents(clip, clipEvents);
        UnregisterClipEvents(rightClip, rightClipEvents);
    }

    private void UnregisterClipEvents(AnimationClip targetClip, AnimationEvent[] events)
    {
        if (targetClip == null || events == null)
            return;

        if (events.Length > 0)
            animEvent.UnregisterFrame(targetClip, events[0], DrinkPotion);
        if (events.Length > 1)
            animEvent.UnregisterFrame(targetClip, events[1], FinishDrink);
    }

    public override bool CanEnter()
    {
        if (owner.CurrentState == null)
            return false;

        // 갈증으로 포션을 잡는 순간에는 줍기/꺼내기 액션이 아직 진행 중일 수 있다.
        if (owner.CurrentActionState != null
            && owner.CurrentActionState.Type != FSM.StateType.Pick
            && owner.CurrentActionState.Type != FSM.StateType.Take)
            return false;

        if (owner.CurrentState.Type != FSM.StateType.Idle && owner.CurrentState.Type != FSM.StateType.Walk)
            return false;

        return hand.FocusedPotion != null;
    }

    public override void Enter()
    {
        base.Enter();
        drinkingSide = hand.FocusedSide;
        drinkingPotion = hand.GetPotion(drinkingSide);
        owner.Animator.SetTrigger(drinkingSide == Hand.Side.Left ? drinkLeftHash : drinkRightHash);
    }

    private void DrinkPotion(AnimationEvent animationEvent)
    {
        if (drinkingPotion == null || hand.GetPotion(drinkingSide) != drinkingPotion)
            return;

        hand.Drink(drinkingSide);
    }

    private void FinishDrink(AnimationEvent animationEvent)
    {
        // 마신 효과로 Hit/Dead 상태로 바뀌었다면 그 상태를 지우지 않는다.
        if (owner.CurrentActionState != this)
            return;

        owner.ClearActionState();
        if (hand.FocusedPotion == null && hand.OtherPotion != null)
            hand.SwitchFocus();
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(drinkLeftHash);
        owner.Animator.ResetTrigger(drinkRightHash);
        drinkingPotion = null;
    }
}
