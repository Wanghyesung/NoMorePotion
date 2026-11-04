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
    private readonly Quaternion defaultViewRotation;
    #endregion

    #region AnimationData
    private readonly AnimationClip rightClip;
    private AnimationEvent[] rightClipEvents;
    #endregion

    #region RuntimeState
    private bool isAiming;
    private bool isThrowing;
    private Transform throwingPotion; //이거도 포션으로 바꿔야함

    #endregion

    public PlayerThrowState(FSM owner, SOThrowState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        throwAim = owner.GetComponentInChildren<ThrowAim>(true);
        defaultViewRotation = owner.PlayerView.localRotation;
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

        Instance.Skill0Action.performed += RequestThrow;
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
            Instance.Skill0Action.performed -= RequestThrow;
            Instance.AimAction.performed -= RequestAim;
            Instance.AimAction.canceled -= CancelAim;
        }
    }

    public override void UpdateState()
    {
        if (!isAiming || !throwAim.HasTarget)
            return;

        Transform playerView = owner.PlayerView;

        Vector3 direction = throwAim.TargetPosition - playerView.position;
        direction.y = 0f; // 몸이 위아래로 기울지 않도록 수평 회전만

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        playerView.rotation = targetRotation;
    }

    private void RequestThrow(InputAction.CallbackContext context)
    {
        if (owner.CurrentActionState == this)
        {
            if (isAiming)
            {
                isAiming = false;
                owner.LockRotate(this, false);
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
        if (!owner.ChangeActionState(FSM.StateType.Throw))
        {
            isAiming = false;
        }
    }

    private void CancelAim(InputAction.CallbackContext context)
    {
        if (isThrowing == true)
            return;

        owner.ClearActionState();
    }


    private void ThrowWait(AnimationEvent animationEvent)
    {
        if (!isAiming)
            return;

        owner.Animator.SetFloat(AnimationSpeedHash, 0f);
        throwAim.Show(throwingPotion);
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
        if (isAiming)
            owner.LockRotate(this, true);
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
        owner.LockRotate(this, false);
        owner.PlayerView.localRotation = defaultViewRotation;
        owner.Animator.ResetTrigger(throwLeftHash);
        owner.Animator.ResetTrigger(throwRightHash);

        throwAim.Hide();
        isAiming = false;
        isThrowing = false;
        throwingPotion = null;
    }
}
