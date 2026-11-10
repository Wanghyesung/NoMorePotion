using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEngine.InputManagerEntry;
using static UnityEngine.InputSystem.InputAction;
using SF = UnityEngine.SerializeField;
public class CombinationController : ItemInventory
{
    [Serializable]
    public class CombineSlot
    {
        public Image image;
        public InventoryIcon boundSlot;
        public int id;
        public int instanceId;

        public void Init(Sprite image, int id, int instanceId, InventoryIcon slot)
        {
            this.image.sprite = image;
            this.id = id;
            this.instanceId = instanceId;
            boundSlot = slot;
        }

        public void Clear()
        {
            image.sprite = null;
            id = 0;
            instanceId = 0;
            boundSlot.SetAlpha(true);
            boundSlot = null;
        }
    }

    [SF] private CombineSlot leftSlot;
    [SF] private CombineSlot rightSlot;
    [SF] private CombineSlot resultSlot;

    private bool leftFull;
    private bool rightFull;

    private readonly ContainerRef publicInvenCRef = new()
    {
        Id = -1,
        Kind = ContainerKind.Personal,
        Slot = -1
    };

    InputAction combineAction;

    private void OnEnable()
    {
        combineAction = new InputAction(
            name: "Close",
            binding: "<Keyboard>/space",
            type: InputActionType.Button
        );
        combineAction.performed += PotionCraftBtnCallback;

        combineAction.Enable();
    }

    private void OnDisable()
    {
        if (combineAction != null)
        {
            combineAction.Disable();
            combineAction.performed -= PotionCraftBtnCallback;

            combineAction.Dispose();
            combineAction = null;
        }
    }

    public void BoundSlot(int id, int instanceId, InventoryIcon slot)
    {
        if(PotionDataManager.Instance.TryGetMaterialData(id, out var data))
        {
            if (!leftFull)
            {
                leftSlot.Init(data.materialIcon,id,instanceId, slot);
                leftFull = true;
            }    
            else if(!rightFull) 
            {
                if (!PotionDataManager.Instance.TryGetMaterialData(id, out var _))
                    return;
                rightSlot.Init(data.materialIcon, id, instanceId, slot);
                rightFull = true;
            }
        } 
        else if(PotionDataManager.Instance.TryGetPotionData(id, out var potion))
        {
            if (!leftFull)
            {
                leftSlot.Init(potion.potionIcon, id, instanceId, slot);
                leftFull = true;
            }
            else if (!rightFull)
            {
                if (!PotionDataManager.Instance.TryGetPotionData(id, out var _))
                    return;

                rightSlot.Init(potion.potionIcon, id, instanceId, slot);
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

    public void PotionCraftBtnCallback(CallbackContext ev)
    {
        PotionCraft();
    }

    public void PotionCraft()
    {
        if (leftSlot.id == 0 || rightSlot.id == 0) return;

        PotionCombination(leftSlot.id, rightSlot.id).Forget();
    }

    public async UniTask PotionCombination(int leftId, int rightId)
    {
        InventoryResult result = await InventoryNetworkSyncManager.Instance.CraftAsync(
            new Ingredient()
            {
                InstanceId = leftSlot.instanceId,
                MaterialId = leftId,
                Source = publicInvenCRef
            },
            new Ingredient()
            {
                InstanceId = rightSlot.instanceId,
                MaterialId = rightId,
                Source = publicInvenCRef
            }
            );

        if (result.Success)
            Debug.Log($"[Craft] 포션 제작 성공 - {result.Outcome.Potion.Data.potionName}");
        else
            Debug.Log($"[Craft] 포션 제작 실패 - {result.Reason}");

        Clear();

    }
}


