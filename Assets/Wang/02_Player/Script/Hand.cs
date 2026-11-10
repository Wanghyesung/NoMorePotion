using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static InputManager;

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

    private TestPotion leftPotion;
    private TestPotion rightPotion;
    [SerializeField] private TestPotion itemPrefab;
    //[SerializeField] private Inventory inventory = new Inventory();
    [SerializeField] private UnityEvent<TestPotion> OnDrink = new UnityEvent<TestPotion>();

    public Side FocusedSide => focusedSide;
    public void Focus(Side side) => focusedSide = side;
    public event Action OnPotionHeld;
    public TestPotion FocusedPotion => GetPotion(focusedSide);
    public TestPotion OtherPotion => GetPotion(focusedSide == Side.Left ? Side.Right : Side.Left);
    public bool IsBothHandsEmpty => leftPotion == null && rightPotion == null;
    public Side TakeSide => FocusedPotion == null
        ? focusedSide : (focusedSide == Side.Left ? Side.Right : Side.Left);

    private Player player;
    private bool started;
    private bool inputSubscribed;

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

    public bool Hold(Side side, TestPotion potion)
    {
        if (GetPotion(side) != null)
            return false;

        potion.Hold(side == Side.Left ? leftSocket : rightSocket);
        SetPotion(side, potion);
        OnPotionHeld?.Invoke();
        return true;
    }

    public bool TakeOutPotion() => TakeOutPotion(TakeSide);

    public bool TakeOutPotion(Side side)
    {
        if (GetPotion(side) != null)
            return false;


        var obj = GameObject.Instantiate(itemPrefab, transform.position, Quaternion.identity);
        return Hold(side, obj);
    }

    public void Throw(Side side, ThrowData throwData)
    {
        TestPotion potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        potion.transform.SetParent(null, true);
        potion.Throw(throwData);
    }

    public void Drink(Side side)
    {
        TestPotion potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        OnDrink.Invoke(potion);
        potion.Drink(transform.position);
    }

    public void Drink() => Drink(focusedSide);

    public TestPotion GetPotion(Side side) => side == Side.Left ? leftPotion : rightPotion;
    private void SetPotion(Side side, TestPotion potion)
    {
        if (side == Side.Left)
            leftPotion = potion;
        else
            rightPotion = potion;
    }
}
