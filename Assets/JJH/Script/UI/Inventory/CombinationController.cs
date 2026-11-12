using Cysharp.Threading.Tasks;
using DG.Tweening;
using Enzeun.Runtime;
using Enzeun.SO;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static UnityEngine.InputSystem.InputAction;
using SF = UnityEngine.SerializeField;
public class CombinationController : ItemInventory
{
    [Serializable]
    public class CombineSlot : IPointerEnterHandler, IPointerExitHandler
    {
        public Image image;
        public RectTransform rect;
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

        public void Clear(Sprite defaultImage)
        {
            image.sprite = defaultImage;
            id = 0;
            instanceId = 0;
            if(boundSlot != null)
                boundSlot.RestoreAlpha();
            boundSlot = null;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            ItemInstance item = new(id, null, InventorySlotKind.Potion);
            if (item != null) InventoryTooltip.Instance.Show(item, rect);
        }

        public void OnPointerExit(PointerEventData e)
        {
            InventoryTooltip.Instance.Hide();
        }
    }

    [Header("슬롯")]
    [SF] private CombineSlot leftSlot;
    [SF] private CombineSlot rightSlot;
    [SF] private CombineSlot resultSlot;

    [Header("텍스트")]
    [SF] private TextMeshProUGUI WarningText;
    [SF] private TextMeshProUGUI successPerText;

    [Header("기타")]
    [SF] private Sprite defaultImage;
    [SF] private Color highColor = new Color32(0xF2, 0xE8, 0xD5, 0xFF);
    [SF] private Color lowColor = new Color32(0xE0, 0x77, 0x5A, 0xFF);
    [SF, Range(0f, 1f)] private float threshold = 0.5f;
    private bool leftFull;
    private bool rightFull;
    private bool? isLow;
    private readonly ContainerRef publicInvenCRef = new()
    {
        Id = -1,
        Kind = ContainerKind.Pack,
        Slot = -1
    };

    InputAction combineAction;

    private CancellationTokenSource warningTextToken;

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

    #region 포션 제작 및 바인딩 관련

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
            warningTextToken?.Cancel();
            warningTextToken = new();

            PotionWarningTextPopup("재료와 포션은 섞을 수 없습니다.",
                warningTextToken.Token).Forget();

            Debug.Log("재료와 포션은 섞을 수 없습니다.");
            return;
        }

        // 같은 포션 한 개를 양쪽에 올리는 것 방지
        if (!isMaterial && instanceId == leftSlot.instanceId) return;

        // 같은 재료를 양쪽에 쓰려면 2개 이상 있어야 함
        if (isMaterial && id == leftSlot.id && slot.Item.ItemCount < 2)
        {
            warningTextToken?.Cancel();
            warningTextToken = new();

            PotionWarningTextPopup("재료가 부족합니다.",
                warningTextToken.Token).Forget();

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
            warningTextToken?.Cancel();
            warningTextToken = new();

            PotionWarningTextPopup("이 조합으로는 만들 수 없습니다.",
                warningTextToken.Token).Forget();

            Debug.Log("이 조합으로는 만들 수 없습니다.");
            
            Clear(); 
        }
    }

    public void Clear()
    {
        leftFull = false;
        rightFull = false;
        leftSlot.Clear(defaultImage);
        rightSlot.Clear(defaultImage);
        resultSlot.Clear(defaultImage);
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

    public async UniTask PotionWarningTextPopup(string text, CancellationToken token)
    {
        // 시작할 때 초기화 (이전 팝업이 어디서 멈췄든 상관없게)
        WarningText.rectTransform.DOKill();
        WarningText.DOKill();
        WarningText.alpha = 1;
        WarningText.rectTransform.localScale = Vector3.one;
        WarningText.rectTransform.anchoredPosition = Vector2.zero;

        WarningText.gameObject.SetActive(true);
        WarningText.SetText(text);

        Sequence seq = DOTween.Sequence()
            .Append(WarningText.rectTransform.DOPunchScale(new(-0.4f, -0.4f, -0.4f), 0.07f))
            .Append(WarningText.rectTransform.DOAnchorPosY(50, 0.25f))
            .Append(WarningText.DOFade(0, 1f))
            .SetUpdate(true)
            .SetLink(WarningText.gameObject);

        bool canceled = await seq.ToUniTask(cancellationToken: token).SuppressCancellationThrow();

        if (!canceled) WarningText.gameObject.SetActive(false);
    }

    /// <summary>
    /// 조합창 칸에서 서버로 보낼 재료 지정을 만듭니다. 개인 인벤토리 칸에서 올렸으면 "내 개인 인벤토리의 그 칸"으로,
    /// 공용 인벤에서 올렸으면 재료/포션 번호로 가리킵니다.
    /// </summary>
    private Ingredient MakeIngredient(CombineSlot slot, int dataId)
    {
        if (slot.boundSlot != null && slot.boundSlot.isPersonalSlot)
        {
            return new Ingredient
            {
                Source = ContainerRef.Personal(InvenManager.LocalActor, slot.boundSlot.slotNum)
            };
        }

        return new Ingredient
        {
            InstanceId = slot.instanceId,
            MaterialId = dataId,
            Source = publicInvenCRef
        };
    }

    public async UniTask PotionCombination(int leftId, int rightId)
    {
        // 요청을 보내는 동안 조합창이 비워질 수 있으니 출처는 미리 만들어 둡니다.
        Ingredient left = MakeIngredient(leftSlot, leftId);
        Ingredient right = MakeIngredient(rightSlot, rightId);

        int keywordId = PotionCrafter.RollTrapKeywordId();
        InventoryResult result = 
            await InventoryNetworkSyncManager.Instance.CraftAsync(left, right, keywordId);

        if (result.Success)
            Debug.Log($"[Craft] 포션 제작 성공 - {result.Outcome.Potion.Data.potionName}");
        else
            Debug.Log($"[Craft] 포션 제작 실패 - {result.Reason}");

        Clear();
    }
    #endregion

    /// <summary>
    /// 포션의 성공 확률 표시 텍스트 갱신 함수
    /// </summary>
    /// <param name="type">false일 경우 0으로 초기화</param>
    public void RefreshSuccessPerText(bool type = true)
    {
        int successPer = 0;
        if(type)
            successPer = 100 - PotionCrafter.GetPlayerCraftFailChance();
        
        successPerText.SetText($"{successPer}%");

        bool low = successPer / 100 <= threshold;

        if (isLow == low) return;

        isLow = low;
        successPerText.color = low ? lowColor : highColor;
    }

    public void ChangeQuickCombinedDisable()
    {
        combineAction.Disable();
    }

    public void ChangeQuickCombinedEnable()
    {
        combineAction.Enable();
    }

    private void OnDestroy()
    {
        warningTextToken?.Cancel();
        warningTextToken?.Dispose();
    }
}


