using UnityEngine;

/// <summary>씬의 UnityEvent에서 런타임 InputManager를 호출하기 위한 연결 컴포넌트입니다.</summary>
public sealed class GuideInputController : MonoBehaviour
{
    public void LockAllKeys() => InputManager.Instance.LockAllKeys();
    public void UnLockAllKeys() => InputManager.Instance.UnLockAllKeys();
    public void LockMoveKey() => InputManager.Instance.LockMoveKey();
    public void UnLockMoveKey() => InputManager.Instance.UnLockMoveKey();
    public void UnLockJumpKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Jump);
    public void LockLookKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.Look);
    public void UnLockLookKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Look);
    public void LockTakePotionKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.TakePotion);
    public void UnLockTakePotionKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.TakePotion);
    public void LockUsePotionKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.UsePotion);
    public void UnLockUsePotionKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.UsePotion);
    public void UnLockAimKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Aim);
    public void UnLockQuickSlotKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.QuickSlot);
    public void UnLockSwitchHandKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.SwitchHand);
    public void UnLockShakeKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Shake);
    public void LockInteractKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.Interact);
    public void UnLockInteractKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Interact);
    public void SetAllowedKeys(InputManager.GameplayKeys keys) => InputManager.Instance.SetGuideAllowedKeys(keys);
    public void LockKeys(InputManager.GameplayKeys keys) => InputManager.Instance.LockKeys(keys);
    public void UnLockKeys(InputManager.GameplayKeys keys) => InputManager.Instance.UnLockKeys(keys);
    public void ShowCursor() => InputManager.Instance.FocusCursor();
    public void HideCursor() => InputManager.Instance.UnfocusCursor();
}
