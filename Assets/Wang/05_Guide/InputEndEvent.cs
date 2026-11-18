using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InputEndEvent : GuideEndEvent
{
    [Tooltip("TakeOutPotion 등 InputManager와 같은 InputAction을 지정합니다. 활성화/잠금은 InputManager가 관리합니다.")]
    [SerializeField] private InputActionReference inputAction;
    private InputAction subscribedAction;

    protected override void OnBegin()
    {
        subscribedAction = inputAction.action;
        subscribedAction.performed += OnPerformed;
    }

    protected override void OnStop()
    {
        if (subscribedAction != null)
            subscribedAction.performed -= OnPerformed;
        subscribedAction = null;
    }

    private void OnPerformed(InputAction.CallbackContext context) => Complete();
}
