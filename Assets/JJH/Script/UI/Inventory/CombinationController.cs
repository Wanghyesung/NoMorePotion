using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using System;
using TMPro;
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
            if(boundSlot != null)
                boundSlot.SetAlpha(true);
            boundSlot = null;
        }
    }

    [SF] private CombineSlot leftSlot;
    [SF] private CombineSlot rightSlot;
    [SF] private CombineSlot resultSlot;
    [SF] private TextMeshProUGUI WarningTest;
    private bool leftFull;
    private bool rightFull;

    private readonly ContainerRef publicInvenCRef = new()
    {
        Id = -1,
        Kind = ContainerKind.Pack,
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
        var dm = PotionDataManager.Instance;

        bool isMaterial = dm.TryGetMaterialData(id, out var material);
        PotionData potionData = null;
        if (!isMaterial && !dm.TryGetPotionData(id, out potionData)) return;

        Sprite icon = isMaterial ? material.materialIcon : potionData.potionIcon;

        if (!leftFull)
        {
            leftSlot.Init(icon, id, instanceId, slot);
            leftFull = true;
            return;
        }
        if (rightFull) return;

        // 왼쪽과 종류가 다르면 거부 (재료 + 포션 섞기 금지)
        if (dm.TryGetMaterialData(leftSlot.id, out _) != isMaterial)
        {
            Debug.Log("재료와 포션은 섞을 수 없습니다.");
            return;
        }

        // 같은 포션 한 개를 양쪽에 올리는 것 방지
        if (!isMaterial && instanceId == leftSlot.instanceId) return;

        // 같은 재료를 양쪽에 쓰려면 2개 이상 있어야 함
        if (isMaterial && id == leftSlot.id && slot.Item.ItemCount < 2)
        {
            Debug.Log("재료가 부족합니다.");
            return;
        }

        rightSlot.Init(icon, id, instanceId, slot);
        rightFull = true;

        PotionData result = isMaterial
            ? dm.GetPotionDataFromRecipe(leftSlot.id, rightSlot.id)
            : dm.GetUpgradedPotion(leftSlot.id, rightSlot.id);

        if (result != null)
        {
            resultSlot.image.sprite = result.potionIcon;
            resultSlot.id = result.potionID;
        }
        else
        {
            Debug.Log("이 조합으로는 만들 수 없습니다.");
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
            new()
            {
                InstanceId = leftSlot.instanceId,
                MaterialId = leftId,
                Source = publicInvenCRef
            },
            new()
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


