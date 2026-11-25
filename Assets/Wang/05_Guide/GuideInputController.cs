using UnityEngine;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine.Events;

/// <summary>씬의 UnityEvent에서 런타임 InputManager를 호출하기 위한 연결 컴포넌트입니다.</summary>
public sealed class GuideInputController : MonoBehaviour
{
    [SerializeField] private UnityEvent treePotionPrepared = new();
    [SerializeField] private InventoryController inventory;

    // Tab으로 열기는 허용하고, 열리고 나면 실습이 끝날 때까지 Tab을 잠근다.
    public void BeginInventoryGuide()
    {
        inventory.OnOpened += LockTabKey;
        if (inventory.IsOpen)
            LockTabKey();
        else
            UnLockTabKey();
    }

    public void EndInventoryGuide()
    {
        inventory.OnOpened -= LockTabKey;
        LockTabKey();
    }

    public void CloseInventory()
    {
        if (inventory.IsOpen)
            inventory.InventoryActive(destroyCancellationToken).Forget();
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.OnOpened -= LockTabKey;
    }

    public void ClearPotion() => GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>().ClearPotion();

    public void RestorePlayerStatus() => GameSessionManager.Instance.LocalPlayer.GetComponent<Player>().RestorePlayerStatus();

    // 개인 슬롯 연습을 정리한 뒤, 다음 나무 단계에서 쓸 포션을 준비한다.
    public void PrepareTreePotion(PotionData potion) => PrepareTreePotionAsync(potion).Forget();

    private async UniTask PrepareTreePotionAsync(PotionData potion)
    {
        var token = destroyCancellationToken;
        InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
        PersonalInventory personal = manager.State.GetPersonal(manager.LocalActor);
        if (personal == null)
            return;

        var requests = new List<UniTask<InventoryResult>>();
        for (int i = 0; i < personal.Capacity; i++)
            if (personal.Slots[i].Kind == InventorySlotKind.Potion)
                requests.Add(manager.TakeOutQuickSlotAsync(i));

        // 제거 응답을 전부 받은 다음 지급해야 새 포션이 함께 지워지지 않는다.
        InventoryResult[] results = await UniTask.WhenAll(requests);
        if (token.IsCancellationRequested)
            return;
        foreach (InventoryResult result in results)
            if (!result.Success)
            {
                Debug.LogError($"[GuideInputController] 개인 포션 정리 실패: {result.Reason}", this);
                return;
            }

        // 화면의 4번 퀵슬롯은 배열 인덱스 3이다.
        InventoryResult granted = await manager.GrantPotionAsync(
            ContainerRef.Personal(manager.LocalActor, 3), potion.potionID);
        if (token.IsCancellationRequested)
            return;
        if (!granted.Success)
        {
            Debug.LogError($"[GuideInputController] 벌목 포션 지급 실패: {granted.Reason}", this);
            return;
        }

        // 지급 완료 후에만 UnityEvent로 인벤토리 상호작용을 끈다.
        treePotionPrepared.Invoke();
    }

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
    public void LockThrowPotionKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.ThrowPotion);
    public void UnLockThrowPotionKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.ThrowPotion);
    public void LockDrinkPotionKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.DrinkPotion);
    public void UnLockDrinkPotionKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.DrinkPotion);
    public void UnLockAimKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Aim);
    public void UnLockQuickSlotKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.QuickSlot);
    public void UnLockQuickSlot1Key() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.QuickSlot1);
    public void UnLockQuickSlot4Key() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.QuickSlot4);
    public void UnLockSwitchHandKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.SwitchHand);
    public void UnLockShakeKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Shake);
    public void LockInteractKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.Interact);
    public void UnLockInteractKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Interact);
    public void LockTabKey() => InputManager.Instance.LockKeys(InputManager.GameplayKeys.Tab);
    public void UnLockTabKey() => InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.Tab);
    public void SetAllowedKeys(InputManager.GameplayKeys keys) => InputManager.Instance.SetGuideAllowedKeys(keys);
    public void LockKeys(InputManager.GameplayKeys keys) => InputManager.Instance.LockKeys(keys);
    public void UnLockKeys(InputManager.GameplayKeys keys) => InputManager.Instance.UnLockKeys(keys);
    public void ShowCursor() => InputManager.Instance.FocusCursor();
    public void HideCursor() => InputManager.Instance.UnfocusCursor();
}
