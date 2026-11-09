using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using SF = UnityEngine.SerializeField;

public class PrivateInventory : MonoBehaviour
{
    [SF] private List<InventoryIcon> privateInventories;

    /// <summary>
    /// 개인 인벤토리 슬롯에 넣을 때
    /// </summary>
    public void GetItem(ItemInstance data)
    {
        foreach (var item in privateInventories)
        {
            if(item.Item == null)
            {
                item.Init(data);
                break;
            }
        }
    }

    /// <summary>
    /// 특정 개인 인벤토리 슬롯에 직접 넣을 때
    /// </summary>
    /// <param name="slotNum"></param>
    public void GetItem(ItemInstance data, int slotNum)
    {
        privateInventories[slotNum].Init(data);
    }

    /// <summary>
    /// 공용 인벤토리 혹은 창고로 이동할 때 사용하는 함수
    /// </summary>
    /// <param name="slotNum"></param>
    /// <param name="moveType"></param>
    public void MoveItem(int slotNum, bool moveType = false)
    {

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
