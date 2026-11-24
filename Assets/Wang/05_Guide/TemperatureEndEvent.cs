using System.Threading;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;

// 낮은 체온에서 포션으로 회복하는 연습. 종료하거나 중단하면 원래 체온으로 복원한다.
public sealed class TemperatureEndEvent : GuideEndEvent
{
    [SerializeField] private BodyTemperatureManager temperature;
    [SerializeField] private PotionData tutorialPotion;
    [SerializeField, Range(0, 100)] private int tutorialTemperature = 20;

    private Hand subscribedHand;
    private CancellationTokenSource grantCancellation;
    private int previousTemperature;
    private bool drank;

    // 앞 단계의 Start Event에서 체온만 낮춘다. 포션 지급은 다음 단계에서 시작한다.
    public void LowerTemperature()
    {
        previousTemperature = temperature.bodyTemperature;
        SetTemperature(tutorialTemperature);
    }

    protected override void OnBegin()
    {
        drank = false;
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.ClearPotion();
        subscribedHand.OnPotionHeld += UpdateGuideKey;
        subscribedHand.OnPotionDrunk += OnDrunk;
        temperature.OnBodyTemperatureStateChanged += OnTemperatureChanged;
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
                Debug.LogError($"[TemperatureEndEvent] 1번 슬롯 비우기 실패: {moved.Reason}", this);
                return;
            }
        }

        InventoryResult result = await manager.GrantPotionAsync(slot, tutorialPotion.potionID);
        if (token.IsCancellationRequested)
            return;
        if (!result.Success)
        {
            Debug.LogError($"[TemperatureEndEvent] 연습 포션 지급 실패: {result.Reason}", this);
            return;
        }

        InputManager.Instance.UnLockKeys(InputManager.GameplayKeys.QuickSlot1 | InputManager.GameplayKeys.DrinkPotion);
        UpdateGuideKey();
    }

    private void UpdateGuideKey()
    {
        bool held = !subscribedHand.IsBothHandsEmpty;
        KeyCanvas.Instance.ShowPlayerKey(held ? KeyGuide.MouseLeftHold : KeyGuide.Alpha1, held ? 2 : 1);
    }

    private void OnDrunk()
    {
        drank = true;
        TryComplete();
    }

    private void OnTemperatureChanged(BodyTemperatureState previous, BodyTemperatureState next) => TryComplete();

    private void TryComplete()
    {
        // 마시기 알림과 포션 효과 적용 순서에 관계없이, 둘 다 확인한 뒤 완료한다.
        if (drank && temperature.currentBodyTemperatureState == BodyTemperatureState.Normal)
            Complete();
    }

    protected override void OnStop()
    {
        grantCancellation?.Cancel();
        grantCancellation?.Dispose();
        grantCancellation = null;
        if (subscribedHand != null)
        {
            subscribedHand.OnPotionHeld -= UpdateGuideKey;
            subscribedHand.OnPotionDrunk -= OnDrunk;
        }
        subscribedHand = null;
        if (temperature != null)
        {
            temperature.OnBodyTemperatureStateChanged -= OnTemperatureChanged;
            SetTemperature(previousTemperature);
        }
    }

    private void SetTemperature(int value)
    {
        int amount = value - temperature.bodyTemperature;
        if (amount > 0)
            temperature.IncreaseBodyTemperature(amount);
        else if (amount < 0)
            temperature.DecreaseBodyTemperature(-amount);
    }
}
