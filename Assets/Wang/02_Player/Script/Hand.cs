using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static InputManager;

[DisallowMultipleComponent]
public sealed class Hand : MonoBehaviour
{
    public enum Side { Left, Right }

    [SerializeField] private Side focusedSide;
    [SerializeField] private Transform leftSocket;
    [SerializeField] private Transform rightSocket;
    [SerializeField] private UnityEvent<GameObject> OnDrink = new UnityEvent<GameObject>(); //이거 Potion으로 바꾸기
    public Side FocusedSide => focusedSide;
    public Transform FocusedPotion => GetPotion(focusedSide); // 이거도 포션 객체로 바꿔야함

    private void OnEnable()
    {
        Instance.SwitchHandAction.performed += switchFocus;
    }

    private void OnDisable() => Instance.SwitchHandAction.performed -= switchFocus;

    private void switchFocus(InputAction.CallbackContext context) => 
        focusedSide = focusedSide == Side.Left ? Side.Right : Side.Left;

    public void Hold(Side side, Transform potion)
    {
        if (GetPotion(side) != null)
            return;

        potion.transform.SetParent(side == Side.Left ? leftSocket : rightSocket, false);
        potion.transform.localPosition = Vector3.zero;
        potion.transform.localRotation = Quaternion.identity;
        SetPotion(side, potion);
    }

    public void Throw(Side side)
    {
        var potion = GetPotion(side);

        SetPotion(side, null);
    }

    public void Drink(Side side)
    {
        Transform potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        OnDrink.Invoke(potion.gameObject);
        Destroy(potion.gameObject);
    }

    public void Drink() => Drink(focusedSide);

    public Transform GetPotion(Side side) => side == Side.Left ? leftSocket : rightSocket;
    private void SetPotion(Side side, Transform potion)
    {
        if (side == Side.Left)
            leftSocket = potion;
        else
            rightSocket = potion;
    }
}
