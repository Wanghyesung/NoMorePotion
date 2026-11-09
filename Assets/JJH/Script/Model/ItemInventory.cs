using System.Runtime.InteropServices;
using UnityEngine;
using SF = UnityEngine.SerializeField;
public class ItemInventory : MonoBehaviour
{
    [SF] protected InventoryController inven;
    [SF] protected CombinationController potionCombined;

    public void Awake()
    {
        if (inven == null)
            inven = GetComponentInParent<InventoryController>();
    }

}
