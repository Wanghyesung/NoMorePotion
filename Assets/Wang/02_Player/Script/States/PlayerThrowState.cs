using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

/*///////////////////////////////////////////
               PlayerThrowState
기능 : 일반 던지기와 조준 대기의 입력·애니메이션 이벤트를 처리
       조준선 표시와 재생 속도를 제어하고 상태 종료 시 정리
 *///////////////////////////////////////////

public sealed class PlayerThrowState : PlayerState
{
    #region AnimationParameters
    private static readonly int throwLeftHash = Animator.StringToHash("ThrowLeft");
    private static readonly int throwRightHash = Animator.StringToHash("ThrowRight");
    private static readonly int upperBodyAnimationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => upperBodyAnimationSpeedHash;

    #endregion

    #region Components
    private readonly Hand hand;
    private readonly ThrowAim throwAim;
    #endregion

    #region AnimationData
    private readonly AnimationClip rightClip;
    private AnimationEvent[] rightClipEvents;
    #endregion

    #region RuntimeState
    private bool isAiming;
    public bool IsAiming => isAiming;
    private bool isThrowing;
    private PotionMover throwingPotion;
    private Hand.Side throwingSide;
    private ThrowData throwData;
    private bool hasThrown;

    #endregion

    public PlayerThrowState(FSM owner, SOThrowState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        throwAim = owner.GetComponentInChildren<ThrowAim>(true);
        rightClip = data.RightClip;
    }

    public override void Init()
    {
        base.Init();

        rightClipEvents = rightClip.events;
        animEvent.RegisterFrame(clip, clipEvents[0], ThrowWait);
        animEvent.RegisterFrame(clip, clipEvents[1], ThrowPotion);
        animEvent.RegisterFrame(clip, clipEvents[2], FinishThrow);

        animEvent.RegisterFrame(rightClip, rightClipEvents[0], ThrowWait);
        animEvent.RegisterFrame(rightClip, rightClipEvents[1], ThrowPotion);
        animEvent.RegisterFrame(rightClip, rightClipEvents[2], FinishThrow);

        Instance.AimAction.performed += RequestAim;
        Instance.AimAction.canceled += CancelAim;

    }

    public override void Dispose()
    {
        animEvent.UnregisterFrame(clip, clipEvents[0], ThrowWait);
        animEvent.UnregisterFrame(clip, clipEvents[1], ThrowPotion);
        animEvent.UnregisterFrame(clip, clipEvents[2], FinishThrow);

        animEvent.UnregisterFrame(rightClip, rightClipEvents[0], ThrowWait);
        animEvent.UnregisterFrame(rightClip, rightClipEvents[1], ThrowPotion);
        animEvent.UnregisterFrame(rightClip, rightClipEvents[2], FinishThrow);


        if (Instance != null)
        {
            Instance.AimAction.performed -= RequestAim;
            Instance.AimAction.canceled -= CancelAim;
        }
    }

    public void RequestThrow()
    {
        if (owner.CurrentActionState == this)
        {
            if (isAiming == true)
            {
                if (throwAim.HasTarget == false)
                    return;

                throwData = throwAim.ThrowData;
                isAiming = false;
                isThrowing = true;
                owner.Animator.SetFloat(AnimationSpeedHash, data.AnimationSpeed);
                throwAim.Hide();
            }
            return;
        }

        isAiming = false;
        owner.ChangeActionState(FSM.StateType.Throw);
    }

    private void RequestAim(InputAction.CallbackContext context)
    {
        if (owner.CurrentActionState == this)
            return;

        isAiming = true;
        if (owner.ChangeActionState(FSM.StateType.Throw) == false)
        {
            isAiming = false;
        }
    }

    private void CancelAim(InputAction.CallbackContext context)
    {
        if (owner.CurrentActionState != this || isThrowing == true)
            return;

        owner.ClearActionState();
    }


    private void ThrowWait(AnimationEvent animationEvent)
    {
        if (isAiming == false)
            return;

        owner.Animator.SetFloat(AnimationSpeedHash, 0f);
        throwAim.Show(throwingPotion.transform);
    }

    public override bool CanEnter()
    {
        var currentType = owner.CurrentState.Type;
        if (currentType != FSM.StateType.Idle && currentType != FSM.StateType.Walk)
            return false;

        if (hand.FocusedPotion == null)
            return false;

        if (isAiming == false)
            return throwAim.TryCreateThrowData(hand.FocusedPotion.transform.position, out throwData);

        return true;
    }

    public override void Enter()
    {
        base.Enter();
        // 조준 중에도 FSM의 마우스 입력으로 Player 루트와 카메라가 함께 회전한다.
        owner.PlayerView.localRotation = Quaternion.identity;
        throwingSide = hand.FocusedSide;
        throwingPotion = hand.GetPotion(throwingSide);
        hasThrown = false;
        isThrowing = isAiming == false;

        owner.Animator.SetTrigger(hand.FocusedSide == Hand.Side.Left ? throwLeftHash : throwRightHash);
    }


    private void ThrowPotion(AnimationEvent animationEvent)
    {
        if (hasThrown == true || throwingPotion == null || hand.GetPotion(throwingSide) != throwingPotion)
            return;

        hasThrown = true;
        hand.Throw(throwingSide, throwData.WithOrigin(throwingPotion.transform.position));
    }

    private void FinishThrow(AnimationEvent animationEvent)
    {
        owner.ClearActionState();

        if(hand.OtherPotion != null)
            hand.SwitchFocus();
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(throwLeftHash);
        owner.Animator.ResetTrigger(throwRightHash);

        throwAim.Hide();
        isAiming = false;
        isThrowing = false;
        throwingPotion = null;
    }
}
