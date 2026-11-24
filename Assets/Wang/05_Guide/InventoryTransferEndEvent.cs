using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using System.Threading;

// 지정한 방향으로 포션을 옮기면 잠시 뒤 인벤토리를 닫고 다음 안내로 넘어간다.
public sealed class InventoryTransferEndEvent : GuideEndEvent
{
    [FormerlySerializedAs("storageTransfer")]
    [SerializeField] private InventoryTransferRelay transfer;
    [SerializeField] private ContainerKind destination = ContainerKind.Pack;
    [SerializeField] private InventoryController inventory;
    [SerializeField, Min(0f)] private float closeDelaySeconds = 1f;
    [SerializeField] private PotionData[] tutorialPotions;
    [FormerlySerializedAs("movedToPackEvent")]
    [SerializeField] private UnityEvent transferredEvent = new();
    [SerializeField] private UnityEvent stoppedEvent = new();

    private CancellationTokenSource closeCancellation;

    protected override void OnBegin()
    {
        transfer.OnPotionTransferred += OnTransferred;
    }

    public void PreparePotions()
    {
        if (GameAuthority.HasAuthority)
            GrantPotions().Forget();
    }

    private async UniTask GrantPotions()
    {
        foreach (PotionData potion in tutorialPotions)
        {
            if (!IsListening)
                return;

            InventoryResult result = await InventoryNetworkSyncManager.Instance
                .GrantPotionAsync(ContainerRef.StorageRef, potion.potionID);
            if (!result.Success)
            {
                Debug.LogError($"[InventoryTransferEndEvent] 연습 포션 지급 실패: {result.Reason}", this);
                return;
            }
        }
    }

    private void OnTransferred(ContainerKind to)
    {
        if (to != destination || closeCancellation != null)
            return;

        closeCancellation = new CancellationTokenSource();
        transferredEvent.Invoke();
        ShowGuideText(1);
        CloseInventory(closeCancellation.Token).Forget();
    }

    private async UniTask CloseInventory(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(System.TimeSpan.FromSeconds(closeDelaySeconds),
            ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow();
        if (canceled)
            return;

        // InventoryActive는 토글이므로 이미 닫았다면 다시 열지 않는다.
        if (inventory.IsOpen)
            await inventory.InventoryActive(token);
        if (!token.IsCancellationRequested)
            Complete();
    }

    protected override void OnStop()
    {
        transfer.OnPotionTransferred -= OnTransferred;
        closeCancellation?.Cancel();
        closeCancellation?.Dispose();
        closeCancellation = null;
        stoppedEvent.Invoke();
    }
}
