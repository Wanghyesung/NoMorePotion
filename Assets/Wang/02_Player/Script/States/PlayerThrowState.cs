using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

public sealed class PlayerThrowState : PlayerState
{
    private static readonly int throwLeftHash = Animator.StringToHash("ThrowLeft");
    private static readonly int throwRightHash = Animator.StringToHash("ThrowRight");
    private static readonly int upperBodyAnimationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => upperBodyAnimationSpeedHash;

    private readonly Hand hand;
    private readonly AnimationClip rightClip;
    private AnimationEvent[] rightClipEvents;
    private readonly float throwSpeed;
    private Transform throwingPotion; //이거도 포션으로 바꿔야함

    public PlayerThrowState(FSM owner, SOThrowState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        throwSpeed = data.ThrowSpeed;
        rightClip = data.RightClip;
    }

    public override void Init()
    {
        base.Init();

        rightClipEvents = rightClip.events;
        animEvent.RegisterFrame(clip, clipEvents[0], ThrowPotion);
        animEvent.RegisterFrame(clip, clipEvents[1], FinishThrow);

        animEvent.RegisterFrame(rightClip, rightClipEvents[0], ThrowPotion);
        animEvent.RegisterFrame(rightClip, rightClipEvents[1], FinishThrow);

        Instance.Skill0Action.performed += RequestThrow;
    }

    public override void Dispose()
    {
        animEvent.UnregisterFrame(clip, clipEvents[0], ThrowPotion);
        animEvent.UnregisterFrame(clip, clipEvents[1], FinishThrow);

        animEvent.UnregisterFrame(rightClip, rightClipEvents[0], ThrowPotion);
        animEvent.UnregisterFrame(rightClip, rightClipEvents[1], FinishThrow);


        if (Instance != null)
            Instance.Skill0Action.performed -= RequestThrow;
    }

    private void RequestThrow(InputAction.CallbackContext context)
    {
        owner.ChangeActionState(FSM.StateType.Throw);
    }
    public override bool CanEnter()
    {
        var currentType = owner.CurrentState.Type;
        if (currentType != FSM.StateType.Idle && currentType != FSM.StateType.Walk)
            return false;

        if (hand.FocusedPotion == null)
            return false;

        return true;
    }
        
    public override void Enter()
    {
        base.Enter();
        throwingPotion = hand.GetPotion(hand.FocusedSide);

        owner.Animator.SetTrigger(hand.FocusedSide == Hand.Side.Left ? throwLeftHash : throwRightHash);
    }

    private void ThrowPotion(AnimationEvent animationEvent)
    {
        //타겟으로 걸린 포션 캐싱하고 던지기
    }

    private void FinishThrow(AnimationEvent animationEvent)
    {
        owner.ClearActionState();
    }
    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(throwLeftHash);
        owner.Animator.ResetTrigger(throwRightHash);

        throwingPotion = null;
    }
}
