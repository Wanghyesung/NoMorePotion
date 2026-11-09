using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using MCPForUnity.Editor.Tools;
using System;
using UnityEngine;
using UnityEngine.UI;

using SF = UnityEngine.SerializeField;
public class CombinationController : ItemInventory
{
    [Serializable]
    public class CombineSlot
    {
        public Image image;
        public int id;
        public int instanceId;

        public void Init(Sprite image, int id, int instanceId)
        {
            this.image.sprite = image;
            this.id = id;
            this.instanceId = instanceId;
        }

        public void Clear()
        {
            image = null;
            id = 0;
            instanceId = 0;
        }
    }

    [SF] private CombineSlot leftSlot;
    [SF] private CombineSlot rightSlot;
    [SF] private CombineSlot resultSlot;

    private bool leftFull;
    private bool rightFull;

    public void BoundSlot(int id, int instanceId)
    {
        if(PotionDataManager.Instance.TryGetMaterialData(id, out var data))
        {
            if (!leftFull)
            {
                leftSlot.Init(data.materialIcon,id,instanceId);
                leftFull = true;
            }    
            else if(!rightFull) 
            {
                if (!PotionDataManager.Instance.TryGetMaterialData(id, out var _))
                    return;
                rightSlot.Init(data.materialIcon, id, instanceId);
                rightFull = true;
            }
        } 
        else if(PotionDataManager.Instance.TryGetPotionData(id, out var potion))
        {
            if (!leftFull)
            {
                leftSlot.Init(potion.potionIcon, id, instanceId);
                leftFull = true;
            }
            else if (!rightFull)
            {
                if (!PotionDataManager.Instance.TryGetPotionData(id, out var _))
                    return;

                rightSlot.Init(potion.potionIcon, id, instanceId);
                rightFull = true;
            }
        }

        if(leftFull && rightFull)
        {
            PotionData potion = PotionDataManager.Instance.GetPotionDataFromRecipe(leftSlot.id, rightSlot.id);

            if(potion != null)
            {
                resultSlot.image.sprite = potion.potionIcon; 
                resultSlot.id = id;
            }else
            {
                Debug.Log("이 재료는 조합할 수 없습니다.");
            }
        }
    }

    public void Clear()
    {
        leftFull = false;
        rightFull = false;
        leftSlot.Clear();
        rightSlot.Clear();
        resultSlot.Clear();
    }

    public void PotionCraft()
    {
        PotionCombination(leftSlot.id, rightSlot.id).Forget();
        
    }

    public async UniTask PotionCombination(int leftId, int rightId,bool type = false)
    {
        PotionInstance potion;

        if (!type)
            potion = PotionCrafter.CraftFromMaterials(leftId, rightId);
        else
            potion = PotionCrafter.CraftFromPotion(leftId, rightId);

        if(potion == null)
        {
            Debug.Log("[Craft] 포션 제작 불가, 레피시 없음");
            return;
        }


        InventoryResult result = 
            await InventoryNetworkSyncManager.Instance.AddPotionAsync(potion);

        if (leftSlot.instanceId == 0 && leftSlot.instanceId == 0)
        {
            await InventoryNetworkSyncManager.Instance.RemoveMaterialAsync(leftSlot.id, 1);
            await InventoryNetworkSyncManager.Instance.RemoveMaterialAsync(rightSlot.id, 1);
        }
        else
        {
            // 포션 Remove 부재, 생길 때 다시 작성
        }
        Clear();

    }
}


