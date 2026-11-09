using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using UnityEngine.UI;

using SF = UnityEngine.SerializeField;
public class CombinationController : ItemInventory
{
    public struct Slot
    {
        public Image image;
        public int id;
    }
    [SF] private Slot leftSlot;
    [SF] private Slot rightSlot;
    [SF] private Slot resultSlot;

    public void PotionCombination(int id, int id2,bool type = false)
    {
        
        PotionInstance potion;

        if (!type)
            potion = PotionCrafter.CraftFromMaterials(id, id2);
        else
            potion = PotionCrafter.CraftFromPotion(id, id2);

        if(potion == null)
        {
            Debug.Log("제작 불가");
            return;
        }

        ItemInstance item = new(potion.Data.ID, 
            potion.Data.potionIcon, InventorySlotKind.Potion);

        inven.GetMakePotion(item);
    }
}


