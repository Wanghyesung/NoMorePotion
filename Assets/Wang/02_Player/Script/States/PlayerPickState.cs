using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
                 PlayerPickState
기능 : E 입력 시 반경 안의 대상을 탐지하고 상체 줍기 모션을 재생
       Pick 이벤트에서 대상을 확인하고 종료 시 상태와 회전 잠금을 정리
 *///////////////////////////////////////////

public sealed class PlayerPickState : PlayerState
{
    #region AnimationParameters
    private static readonly int pickHash = Animator.StringToHash("Pick");
    private static readonly int animationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => animationSpeedHash;
    #endregion

    #region Runtime
    private readonly RangeDetector detector;
    private bool hasPicked;
    public GameObject Target { get; private set; }
    #endregion

    public PlayerPickState(FSM owner, SOPickState data) : base(owner, data)
    {
        detector = owner.GetComponent<RangeDetector>();
    }

    public override void Init()
    {
        base.Init();
        InputManager.Instance.InteractAction.performed += RequestPick;
        animEvent.RegisterFrame(clip, clipEvents[0], Pick);
        animEvent.RegisterFrame(clip, clipEvents[1], FinishPick);
    }

    public override void Dispose()
    {
        animEvent.UnregisterFrame(clip, clipEvents[0], Pick);
        animEvent.UnregisterFrame(clip, clipEvents[1], FinishPick);
        InputManager.Instance.InteractAction.performed -= RequestPick;
    }

    private void RequestPick(InputAction.CallbackContext context)
    {
        owner.ChangeActionState(FSM.StateType.Pick);
    }

    public override bool CanEnter()
    {
        if (owner.HasGameplayInput == false || owner.CurrentActionState != null)
            return false;

        if (owner.CurrentState.Type != FSM.StateType.Idle && owner.CurrentState.Type != FSM.StateType.Walk)
            return false;

        Target = detector.Get();
        return Target != null;
    }

    public override void Enter()
    {
        base.Enter();

        hasPicked = false;
        owner.Animator.SetTrigger(pickHash);
    }

    private void Pick(AnimationEvent animationEvent)
    {
        if (hasPicked || Target == null || !Target.activeInHierarchy)
            return;

        hasPicked = true;
        Debug.Log($"Pick: {Target.name}", Target);
    }

    private void FinishPick(AnimationEvent animationEvent)
    {
        owner.ClearActionState();
    }

    public override void Exit()
    {
        base.Exit();
        owner.Animator.ResetTrigger(pickHash);
        Target = null;
        hasPicked = false;
    }
}
