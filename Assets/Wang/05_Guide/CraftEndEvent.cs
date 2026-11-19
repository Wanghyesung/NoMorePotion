using Enzeun.Runtime;

public sealed class CraftEndEvent : GuideEndEvent
{
    protected override void OnBegin()
    {
        InventoryNetworkSyncManager.Instance.OnPotionCrafted += OnPotionCrafted;
    }

    protected override void OnStop()
    {
        if (InventoryNetworkSyncManager.Instance != null)
            InventoryNetworkSyncManager.Instance.OnPotionCrafted -= OnPotionCrafted;
    }

    private void OnPotionCrafted(PotionInstance potion) => Complete();
}
