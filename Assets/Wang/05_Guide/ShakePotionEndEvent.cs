using Enzeun.Runtime;
using Enzeun.SO;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public sealed class ShakePotionEndEvent : GuideEndEvent
{
    [SerializeField] private PotionData tutorialPotion;
    [SerializeField] private TrapPotionKeyword trapKeyword;
    private Hand subscribedHand;
    private CancellationTokenSource grantCancellation;
    private int grantedInstanceId;
    private bool inspected;

    protected override void OnBegin()
    {
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.ClearPotion();
        grantedInstanceId = 0;
        inspected = false;
        subscribedHand.OnPotionHeld += OnPotionHeld;
        subscribedHand.OnShake += OnShake;
        subscribedHand.OnShakeFinished += OnShakeFinished;
        grantCancellation = new CancellationTokenSource();
        GrantPotion(grantCancellation.Token).Forget();
    }

    private async UniTask GrantPotion(CancellationToken token)
    {
        InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
        ContainerRef slot = ContainerRef.Personal(manager.LocalActor, 0);
        PersonalInventory personal = manager.State.GetPersonal(manager.LocalActor);
        if (personal != null && !personal.Slots[0].IsEmpty)
        {
            InventoryResult moved = await manager.TransferAsync(slot, ContainerRef.PackRef);
            if (token.IsCancellationRequested)
                return;
            if (!moved.Success)
            {
                Debug.LogError($"[ShakePotionEndEvent] 1번 슬롯 비우기 실패: {moved.Reason}", this);
                return;
            }
        }

        InventoryResult result = await manager.GrantPotionAsync(slot, tutorialPotion.potionID, trapKeyword.KeywordID);
        if (token.IsCancellationRequested)
            return;
        if (!result.Success)
        {
            Debug.LogError($"[ShakePotionEndEvent] 함정 포션 지급 실패: {result.Reason}", this);
            return;
        }

        grantedInstanceId = result.Outcome.Potion.InstanceId;
        KeyCanvas.Instance.ShowPlayerKey(KeyGuide.Alpha1, 1);
        InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.QuickSlot1 | InputManager.GameplayKeys.Shake);
    }

    private void OnPotionHeld()
    {
        if (IsTutorialPotion(subscribedHand.GetPotion(Hand.Side.Left)) ||
            IsTutorialPotion(subscribedHand.GetPotion(Hand.Side.Right)))
            KeyCanvas.Instance.ShowPlayerKey(KeyGuide.R, 2);
    }

    private bool IsTutorialPotion(PotionMover potion) =>
        potion != null && potion.PotionInstance.InstanceId == grantedInstanceId;

    protected override void OnStop()
    {
        grantCancellation?.Cancel();
        grantCancellation?.Dispose();
        grantCancellation = null;
        if (subscribedHand != null)
        {
            subscribedHand.OnPotionHeld -= OnPotionHeld;
            subscribedHand.OnShake -= OnShake;
            subscribedHand.OnShakeFinished -= OnShakeFinished;
        }
        subscribedHand = null;
    }

    private void OnShake(PotionInstance left, PotionInstance right)
    {
        inspected = IsInspectedTrap(left) || IsInspectedTrap(right);
    }

    private bool IsInspectedTrap(PotionInstance potion) =>
        potion != null && grantedInstanceId != 0 && potion.InstanceId == grantedInstanceId && potion.Inspect(out _);

    private void OnShakeFinished()
    {
        if (inspected)
            Complete();
    }
}
