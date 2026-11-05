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
    [SerializeField] private Inventory inventory = new Inventory();
    [SerializeField] private UnityEvent<TestPotion> OnDrink = new UnityEvent<TestPotion>();

    public Side FocusedSide => focusedSide;
    public TestPotion FocusedPotion => GetPotion(focusedSide);
    public TestPotion OtherPotion => GetPotion(focusedSide == Side.Left ? Side.Right : Side.Left);
    public Side TakeSide => FocusedPotion == null
        ? focusedSide
        : (focusedSide == Side.Left ? Side.Right : Side.Left);

    private void OnEnable()
    {
        Instance.SwitchHandAction.performed += SwitchFocus;
    }

    private void OnDisable()
    {
        if(Instance != null)
            Instance.SwitchHandAction.performed -= SwitchFocus;
    }

    private void SwitchFocus(InputAction.CallbackContext context) => 
        focusedSide = focusedSide == Side.Left ? Side.Right : Side.Left;

    public bool Hold(Side side, TestPotion potion)
    {
        if (GetPotion(side) != null)
            return false;

        potion.transform.SetParent(side == Side.Left ? leftSocket : rightSocket, true);
        potion.transform.localPosition = Vector3.zero;
        potion.transform.localRotation = Quaternion.identity;
        SetPotion(side, potion);
        return true;
    }

    public bool TakeOutPotion()
    {
        if (FocusedPotion != null && OtherPotion != null)
            return false;

        return Hold(TakeSide, inventory.GetItem());
    }

    public void Throw(Side side, ThrowData throwData)
    {
        TestPotion potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        potion.transform.SetParent(null, true);
        potion.GetComponent<ParabolicMovement>().Launch(throwData);
    }

    public void Drink(Side side)
    {
        TestPotion potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        OnDrink.Invoke(potion);
        Destroy(potion.gameObject);
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
