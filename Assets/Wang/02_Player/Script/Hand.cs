using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static InputManager;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;


/*///////////////////////////////////////////
                       Hand
기능 : 포커싱된 손과 손별 보유 포션을 관리하고 소켓에 아이템 연결
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class Hand : MonoBehaviour
{
    public enum Side { Left, Right }

    [SerializeField] private Side focusedSide;
    [SerializeField] private Transform leftSocket;
    [SerializeField] private Transform rightSocket;

    private PotionMover leftPotion;
    private PotionMover rightPotion;
    [SerializeField] private Potion itemPrefab;
    //[SerializeField] private Inventory inventory = new Inventory();
    [SerializeField] private UnityEvent<PotionMover> OnDrink = new UnityEvent<PotionMover>();

    public Side FocusedSide => focusedSide;
    public void Focus(Side side) => focusedSide = side;
    public event Action OnPotionHeld;
    public PotionMover FocusedPotion => GetPotion(focusedSide);
    public PotionMover OtherPotion => GetPotion(focusedSide == Side.Left ? Side.Right : Side.Left);
    public bool IsBothHandsEmpty => leftPotion == null && rightPotion == null;
    public Side TakeSide => FocusedPotion == null
        ? focusedSide : (focusedSide == Side.Left ? Side.Right : Side.Left);

    private Player player;
    private bool started;
    private bool inputSubscribed;
    private bool isTakingOut;

    private void Awake() => player = GetComponent<Player>();

    private void Start()
    {
        started = true;
        SubscribeInput();
    }

    private void OnEnable()
    {
        if (started == true)
            SubscribeInput();
    }

    private void SubscribeInput()
    {
        if (player.IsLocalPlayer == false || inputSubscribed == true)
            return;
        Instance.SwitchHandAction.performed += SwitchFocus;
        inputSubscribed = true;
    }

    private void OnDisable()
    {
        if (inputSubscribed == true && Instance != null)
            Instance.SwitchHandAction.performed -= SwitchFocus;
        inputSubscribed = false;
    }

    private void SwitchFocus(InputAction.CallbackContext context) =>
        focusedSide = focusedSide == Side.Left ? Side.Right : Side.Left;

    public bool Hold(Side side, PotionMover potion)
    {
        if (GetPotion(side) != null)
            return false;

        potion.Hold(side == Side.Left ? leftSocket : rightSocket);
        SetPotion(side, potion);
        OnPotionHeld?.Invoke();
        return true;
    }

    public int EmptyHandCount => (leftPotion == null ? 1 : 0) + (rightPotion == null ? 1 : 0);

    /// <summary>
    /// 인벤토리에서 포션을 무작위로 꺼내 빈 손에 듭니다. (공용 인벤 우선, 없으면 개인 인벤, 없으면 아무 일도 없음)
    /// 빈 손의 수만큼만 꺼내고, 요청을 보냈으면 true 입니다. 결과는 비동기로 손에 반영됩니다.
    /// </summary>
    public bool TakeOutPotion()
    {
        int emptyCount = EmptyHandCount;
        if (emptyCount == 0 || isTakingOut == true)
            return false;

        InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
        if (manager == null)
            return false;

        TakeOutAsync(() => manager.TakeOutRandomAsync(emptyCount)).Forget();
        return true;
    }

    /// <summary>
    /// 개인 인벤토리 퀵슬롯(0~9, 키 1~0)의 포션을 정확히 꺼내 빈 손에 듭니다.
    /// </summary>
    public bool TakeOutQuickSlot(int slot)
    {
        if (EmptyHandCount == 0 || isTakingOut == true)
            return false;

        InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
        if (manager == null)
            return false;

        TakeOutAsync(() => manager.TakeOutQuickSlotAsync(slot)).Forget();
        return true;
    }

    private async UniTaskVoid TakeOutAsync(Func<UniTask<InventoryResult>> request)
    {
        isTakingOut = true;

        try
        {
            InventoryResult result = await request();

            // 기다리는 사이 이 오브젝트가 사라졌으면 더 할 것이 없습니다.
            if (this == null || result.Success == false || result.Outcome.Potions == null)
                return;

            foreach (PotionInstance instance in result.Outcome.Potions)
            {
                // 이미 인벤토리에서 빠진 포션이라 들 손이 없으면 사라집니다. (요청 중에는 새로 꺼낼 수 없어 보통 생기지 않음)
                if (TryGetEmptySide(out Side side) == false)
                {
                    Debug.LogWarning("[Hand] : 포션을 들 빈 손이 없어 꺼낸 포션이 사라졌습니다.");
                    break;
                }

                Potion potion = Instantiate(itemPrefab, transform.position, Quaternion.identity);
                potion.Initialize(instance);
                Hold(side, potion.potionMover);
            }
        }
        finally
        {
            isTakingOut = false;
        }
    }

    // 포커스한 손이 비어 있으면 그 손, 아니면 반대 손을 돌려줍니다. 둘 다 차 있으면 false 입니다.
    private bool TryGetEmptySide(out Side side)
    {
        Side other = focusedSide == Side.Left ? Side.Right : Side.Left;

        if (GetPotion(focusedSide) == null)
        {
            side = focusedSide;
            return true;
        }

        side = other;
        return GetPotion(other) == null;
    }

    public void Throw(Side side, ThrowData throwData)
    {
        PotionMover potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        potion.transform.SetParent(null, true);
        potion.Throw(throwData);
    }

    public void Drink(Side side)
    {
        PotionMover potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        OnDrink.Invoke(potion);
        potion.Drink(transform.position);
    }

    public void Drink() => Drink(focusedSide);

    public PotionMover GetPotion(Side side) => side == Side.Left ? leftPotion : rightPotion;
    private void SetPotion(Side side, PotionMover potion)
    {
        if (side == Side.Left)
            leftPotion = potion;
        else
            rightPotion = potion;
    }
}
