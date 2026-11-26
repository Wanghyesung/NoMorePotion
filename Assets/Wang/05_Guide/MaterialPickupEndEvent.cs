using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;

// 지정한 재료가 공용 인벤토리에 들어오면 완료한다. E 입력만으로는 완료하지 않는다.
public sealed class MaterialPickupEndEvent : GuideEndEvent
{
    [SerializeField] private MaterialData material;
    private InventoryNetworkSyncManager inventory;

    protected override void OnBegin()
    {
        inventory = InventoryNetworkSyncManager.Instance;
        inventory.OnPackItemChanged += OnItemChanged;
    }

    protected override void OnStop()
    {
        if (inventory != null)
            inventory.OnPackItemChanged -= OnItemChanged;
        inventory = null;
    }

    private void OnItemChanged(int kind, int itemId, int delta)
    {
        if (kind == TeamInventoryState.ItemKindMaterial && itemId == material.materialID && delta > 0)
            Complete();
    }
}
