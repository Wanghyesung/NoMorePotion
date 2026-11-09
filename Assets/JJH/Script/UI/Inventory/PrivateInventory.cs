using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using SF = UnityEngine.SerializeField;

public class PrivateInventory : ItemInventory
{
    [SF] private List<InventoryIcon> privateInventories;

    public void OnEnable()
    {
        inven.OnPrivateListAdded += GetItem;
        inven.OnPrivateListRemoved += RemoveItem;
    }

    public void OnDisable()
    {
        inven.OnPrivateListAdded -= GetItem;
        inven.OnPrivateListRemoved -= RemoveItem;
    }

    /// <summary>
    /// 개인 인벤토리 슬롯에 넣을 때
    /// </summary>
    public void GetItem(ItemInstance data)
    {
        InventoryIcon icon = privateInventories.Find(x=> x.Item.itemId == data.itemId);

        if(icon == null)
        {
            foreach (var item in privateInventories)
            {
                if (item.Item == null)
                {
                    item.SetParentInventory(this);
                    item.Init(data);
                    break;
                }
            }
        }
        else
            icon.Item.ItemCount += data.ItemCount;
    }

    /// <summary>
    /// 공용 인벤토리 혹은 창고로 이동할 때 사용하는 함수
    /// </summary>
    /// <param name="slotNum"></param>
    /// <param name="moveType"></param>
    public void MoveItem(int slotNum, bool moveType = false)
    {
        if(moveType)
            inven.GetPublicItem(privateInventories[slotNum].Item);

        RemoveItem(slotNum);
    }

    /// <summary>
    /// 아이템 제거
    /// </summary>
    /// <param name="slotNum"></param>
    public void RemoveItem(int slotNum)
    {
        privateInventories[slotNum].Clear();
    }
}
