using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

public sealed class PlayerThrowState : PlayerState
{
    private static readonly int throwLeftHash = Animator.StringToHash("ThrowLeft");
    private static readonly int throwRightHash = Animator.StringToHash("ThrowRight");
    private readonly Hand hand;
    private readonly float throwSpeed;
    private Transform throwingPotion; //이거도 포션으로 바꿔야함

    public PlayerThrowState(FSM owner, SOThrowState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        throwSpeed = data.ThrowSpeed;
    }

    public override void Init()
    {
        Instance.Skill0Action.performed += requestThrow;
    }

    public override void Dispose()
    {
        if (Instance != null)
            Instance.Skill0Action.performed -= requestThrow;
    }

    private void requestThrow(InputAction.CallbackContext context) => owner.ChangeState(FSM.StateType.Throw);

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
        throwingPotion = hand.GetPotion(hand.FocusedSide);
        owner.StopHorizontalMovement();

        owner.Animator.SetTrigger(hand.FocusedSide == Hand.Side.Left ? throwLeftHash : throwRightHash);
    }

    public void ThrowPotion()
    {
        //타겟으로 걸린 포션 캐싱하고 던지기
    }

    public override void Exit()
    {
        owner.Animator.ResetTrigger(throwLeftHash);
        owner.Animator.ResetTrigger(throwRightHash);
        throwingPotion = null;
    }
}
