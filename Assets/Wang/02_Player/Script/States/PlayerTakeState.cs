using Enzeun.Runtime;
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
    private static readonly int takeBothHash = Animator.StringToHash("TakeBoth");
    private static readonly int animationSpeedHash = Animator.StringToHash("UpperBodyAnimationSpeed");
    protected override int AnimationSpeedHash => animationSpeedHash;
    #endregion

    #region Runtime
    private readonly Hand hand;
    private readonly AnimationClip rightClip;
    private AnimationEvent[] rightClipEvents;
    private readonly AnimationClip bothClip;
    private AnimationEvent[] bothClipEvents;
    private bool takeBoth;
    private bool hasTaken;

    // 현준 수정 : 이번 꺼내기에서 정확히 꺼낼 퀵슬롯 칸입니다. NoQuickSlot 이면 무작위(F 키)
    private const int NoQuickSlot = -1;
    private int quickSlot = NoQuickSlot;
    private System.Action<InputAction.CallbackContext>[] quickSlotHandlers;

    // 현준 수정 : 이번 꺼내기가 다시뽑기(양손이 차 있을 때 F 키)인지 여부입니다.
    private bool reroll;
    #endregion

    public PlayerTakeState(FSM owner, SOTakeState data) : base(owner, data)
    {
        hand = owner.GetComponent<Hand>();
        rightClip = data.RightClip;
        bothClip = data.BothClip;
    }

    public override void Init()
    {
        base.Init();
        rightClipEvents = rightClip.events;
        bothClipEvents = bothClip.events;
        animEvent.RegisterFrame(clip, clipEvents[0], TakePotion);
        animEvent.RegisterFrame(clip, clipEvents[1], FinishTake);
        animEvent.RegisterFrame(rightClip, rightClipEvents[0], TakePotion);
        animEvent.RegisterFrame(rightClip, rightClipEvents[1], FinishTake);
        animEvent.RegisterFrame(bothClip, bothClipEvents[0], TakePotion);
        animEvent.RegisterFrame(bothClip, bothClipEvents[1], FinishTake);

        InputManager.Instance.TakeOutPotionAction.performed += RequestTake;

        // 현준 수정 : 숫자 키 1~0 으로 퀵슬롯(개인 인벤 칸 0~9)의 포션을 꺼냅니다.
        InputAction[] numberActions = GetQuickSlotActions();
        quickSlotHandlers = new System.Action<InputAction.CallbackContext>[numberActions.Length];
        for (int i = 0; i < numberActions.Length; i++)
        {
            int slot = i;
            quickSlotHandlers[i] = context => RequestQuickSlot(slot);
            numberActions[i].performed += quickSlotHandlers[i];
        }
    }

    #region QuickSlot
    // 현준 수정 : 퀵슬롯 칸 번호(0~9) 순서의 입력입니다. 칸 0 = 키 1 ... 칸 8 = 키 9, 칸 9 = 키 0
    private static InputAction[] GetQuickSlotActions()
    {
        InputManager input = InputManager.Instance;
        return new[]
        {
            input.Number1Action, input.Number2Action, input.Number3Action, input.Number4Action, input.Number5Action,
            input.Number6Action, input.Number7Action, input.Number8Action, input.Number9Action, input.Number0Action,
        };
    }

    private void RequestQuickSlot(int slot)
    {
        // 그 칸에 포션이 없으면 꺼내는 동작을 시작하지 않습니다. (애니메이션만 재생되고 아무것도 안 나오는 것을 막음)
        if (HasPotionInQuickSlot(slot) == false)
            return;

        TryEnterTake(slot);
    }

    // 현준 수정 : Enter 가 칸 번호를 읽으므로 먼저 넣고, 들어가지 못하면(이미 꺼내는 중 등) 원래 값으로 되돌립니다.
    // 되돌리지 않으면 꺼내는 도중의 입력이 진행 중인 꺼내기의 칸 번호를 바꿔 버립니다.
    private void TryEnterTake(int slot)
    {
        int previous = quickSlot;
        quickSlot = slot;

        if (owner.ChangeActionState(FSM.StateType.Take) == false)
            quickSlot = previous;
    }

    private static bool HasPotionInQuickSlot(int slot)
    {
        InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
        if (manager == null || manager.State == null || manager.LocalActor <= 0)
            return false;

        PersonalInventory inventory = manager.State.GetPersonal(manager.LocalActor);
        if (inventory == null || slot < 0 || slot >= inventory.Capacity)
            return false;

        return inventory.Slots[slot].Kind == InventorySlotKind.Potion && inventory.Slots[slot].IsEmpty == false;
    }
    #endregion

    public override void Dispose()
    {

        animEvent.UnregisterFrame(clip, clipEvents[0], TakePotion);
        animEvent.UnregisterFrame(clip, clipEvents[1], FinishTake);
        animEvent.UnregisterFrame(rightClip, rightClipEvents[0], TakePotion);
        animEvent.UnregisterFrame(rightClip, rightClipEvents[1], FinishTake);
        animEvent.UnregisterFrame(bothClip, bothClipEvents[0], TakePotion);
        animEvent.UnregisterFrame(bothClip, bothClipEvents[1], FinishTake);
        if (InputManager.Instance != null)
        {
            InputManager.Instance.TakeOutPotionAction.performed -= RequestTake;

            if (quickSlotHandlers != null)
            {
                InputAction[] numberActions = GetQuickSlotActions();
                for (int i = 0; i < numberActions.Length; i++)
                    numberActions[i].performed -= quickSlotHandlers[i];
            }
        }
    }

    private void RequestTake(InputAction.CallbackContext context)
    {
        // F 키는 무작위로 꺼냅니다.
        TryEnterTake(NoQuickSlot);
    }

    public override bool CanEnter()
    {
        if (owner.CurrentActionState != null)
            return false;

        if (owner.CurrentState.Type != FSM.StateType.Idle && owner.CurrentState.Type != FSM.StateType.Walk)
            return false;

        // 현준 수정 : 양손이 모두 차 있어도 F 키는 다시뽑기로 들어갑니다. (퀵슬롯은 빈 손이 있어야 함)
        if (quickSlot == NoQuickSlot && hand.EmptyHandCount == 0)
            return true;

        return hand.FocusedPotion == null || hand.OtherPotion == null;
    }

    public override void Enter()
    {
        base.Enter();
        hasTaken = false;
        // 현준 수정 : 양손이 차 있을 때 F 키는 다시뽑기입니다. (Enter 시점에 정해 두고 TakePotion 에서 씀)
        reroll = quickSlot == NoQuickSlot && hand.EmptyHandCount == 0;
        // 현준 수정 : 퀵슬롯은 포션 한 개만 꺼내므로 양손이 비어도 한 손 동작을 씁니다. (F 키 꺼내기와 다시뽑기는 양손)
        takeBoth = quickSlot == NoQuickSlot && (hand.IsBothHandsEmpty || reroll);
        owner.Animator.SetTrigger(takeBoth == true ? takeBothHash
            : hand.TakeSide == Hand.Side.Left ? takeLeftHash : takeRightHash);
    }

    private void TakePotion(AnimationEvent animationEvent)
    {
        if (hasTaken == true)
            return;

        hasTaken = true;

        // 빈 손의 수만큼 한 번에 요청합니다. (양손이 비었으면 2개, 한 손만 비었으면 1개, 양손이 차 있으면 다시뽑기)
        if (quickSlot != NoQuickSlot)
            hand.TakeOutQuickSlot(quickSlot);
        else if (reroll)
            hand.RerollPotions();
        else
            hand.TakeOutPotion();
    }

    private void FinishTake(AnimationEvent animationEvent) => owner.ClearActionState();

    public override void Exit()
    {
        base.Exit();
        quickSlot = NoQuickSlot;
        reroll = false;
        owner.Animator.ResetTrigger(takeLeftHash);
        owner.Animator.ResetTrigger(takeRightHash);
        owner.Animator.ResetTrigger(takeBothHash);
    }
}
