using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Photon.Pun;
using UnityEngine;
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

        personalInventory.OnSlotChanged -= RefreshInventory;

    }

    /// <summary>
    /// 개인 인벤토리 슬롯에 넣을 때
    /// </summary>
    public void GetItem(ItemInstance data)
    {
        InventoryIcon icon = privateInventories.Find(x => x.Item.itemId == data.itemId);

        if (icon == null)
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
        if (moveType)
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


    // =========== 현준 수정 ======================================
    private PersonalInventory personalInventory;

    public async void Start()
    {
        await CheckPhotonConnect();

        personalInventory = InventoryNetworkSyncManager.Instance.State.Personals[PhotonNetwork.LocalPlayer.ActorNumber];

        BindEvent();

        RefreshInventory();
    }

    public async UniTask<bool> CheckPhotonConnect()
    {

        int count = 0;
        if (!PhotonNetwork.IsConnected)
        {
#if UNITY_EDITOR
            Debug.LogError("[PrivateInventory] : 포톤 네트워크에 연결되지 않았습니다. 개인 인벤토리를 가져올 수 없습니다.");
#endif
            count++;
            if (count >= 10)
            {
                return false;
            }
            await UniTask.WaitForSeconds(1);
        }
        return true;
    }

    public void RefreshInventory()
    {
        int index = 0;
        foreach (var obj in personalInventory.Slots)
        {
            if (obj.Kind == InventorySlotKind.Empty)
            {
                privateInventories[index].SetAlpha(0);
            }
            else
            {
                privateInventories[index].ChangeSlotState(obj);
            }

            index++;
        }
    }

    public void RefreshInventory(int index)
    {
        var obj = personalInventory.Slots[index];
        {
            if (obj.Kind == InventorySlotKind.Empty)
            {
                privateInventories[index].SetAlpha(0);
            }
            else
            {
                privateInventories[index].ChangeSlotState(obj);
            }
        }
    }

    public void BindEvent()
    {
        personalInventory.OnSlotChanged += RefreshInventory;
    }
}
