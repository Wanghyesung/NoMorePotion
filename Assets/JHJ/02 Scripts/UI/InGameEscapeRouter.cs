using UnityEngine;
using UnityEngine.InputSystem;
using SF = UnityEngine.SerializeField;

public class InGameEscapeRouter : MonoBehaviour
{
    [SF] private ConfirmPopupUI confirmPopup;
    [SF] private SettingsPopupUI settingPopup;
    [SF] private InGameChatUI chat;
    [SF] private InventoryController inventory;


    private void OnEnable()
    {
        if(InputManager.Instance == null)
        {
            Debug.LogError("[InGameEscapeRouter] InputManager 없음");
            return;
        }

        InputManager.Instance.EscapeAction.performed += OnEscape;
    }

    private void OnDisable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.EscapeAction.performed -= OnEscape;
    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        if (GameSessionManager.Instance != null && GameSessionManager.Instance.IsEnded)
            return;

        if (confirmPopup != null && confirmPopup.IsOpen)
            confirmPopup.Cancel();

        else if (settingPopup.IsOpen)
            settingPopup.Cancel();

        else if (chat != null && chat.IsTyping)
            return;

        else if (inventory != null && inventory.IsOpen)
            inventory.OnTabClosed();

        else
            settingPopup.Open();
    }
}
