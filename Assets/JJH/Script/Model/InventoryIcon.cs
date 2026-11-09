using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using SF = UnityEngine.SerializeField;

public class InventoryIcon : MonoBehaviour
{

    [SF] private ItemInventory inventory;
    // 아이템 아이콘
    [SF] private Image itemImage;
    // 아이템 개수 텍스트
    [SF] private TextMeshProUGUI itemCountText;
    [SF] private TextMeshProUGUI itemSlotNum;
    public int slotNum = -1;

    public ItemInstance Item { get; private set; }

    private void Awake()
    {
        if(itemSlotNum != null && 
            itemSlotNum.gameObject.activeSelf &&
            slotNum >= 0)
        {
            if(slotNum + 1 < 10)
                itemSlotNum.text = (slotNum + 1).ToString();
            else
                itemSlotNum.text = "0";
        }
            
    }

    public void SetParentInventory(ItemInventory inven)
    {
        inventory = inven;
    }

    public void Init(ItemInstance item)
    {
        // 이전 아이템의 자동 새로고침 구독
        if (Item != null) 
            Item.OnChanged -= Reload;

        Item = item;

        // 자동 새로고침 구독해제
        if (Item != null) 
            Item.OnChanged += Reload;
    }

    /// <summary>
    /// 아이템 정보 초기화
    /// </summary>
    public void Clear() => Init(null);

    /// <summary>
    /// 아이템 정보 갱신
    /// </summary>
    private void Reload()
    {
        bool enabled = Item != null && Item.itemImage != null && Item.ItemCount > 0;

        itemImage.enabled = enabled;
        itemCountText.enabled = enabled;
        itemCountText.text = enabled && 
            Item.ItemCount > 1 ? Item.ItemCount.ToString() : string.Empty;
        

        if (!enabled) return;

        itemImage.sprite = Item.itemImage;
    }

    public void ItemCombination()
    {

    }

    private void OnDestroy()
    {
        if (Item != null) Item.OnChanged -= Reload;
    }
}
