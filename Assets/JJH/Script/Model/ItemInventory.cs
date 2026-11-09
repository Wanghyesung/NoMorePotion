using Enzeun.Runtime;
using System.Runtime.InteropServices;
using UnityEngine;
using SF = UnityEngine.SerializeField;
public class ItemInventory : MonoBehaviour
{
    [SF] protected InventoryController inven;
    public CombinationController potionCombined;

    public InventoryNetworkSyncManager InvenManager
    => InventoryNetworkSyncManager.Instance;

    protected virtual void Awake()
    {
        if (inven == null)
            inven = GetComponentInParent<InventoryController>();
    }

}
