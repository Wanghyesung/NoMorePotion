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
using static JJH_Constants;

using static UnityEngine.InputSystem.InputAction;
using SF = UnityEngine.SerializeField;
public class CombinationController : ItemInventory
{
    [Header("슬롯")]
    [SF] private CombineSlot leftSlot;
    [SF] private CombineSlot rightSlot;
    [SF] private CombineSlot resultSlot;

    [Header("텍스트")]
    [SF] private TextMeshProUGUI WarningText;
    [SF] private TextMeshProUGUI successPerText;

    [Header("분해")]
    [SF] private GameObject combineArrow;
    [SF] private GameObject compositionArrow;
    [SF] private Button compositionBtn;
    [SF] private Sprite btnNormal;
    [SF] private Sprite btnPressed;
    [SF] private TextMeshProUGUI combineAndCompositBtnText;

    [SF] private GameObject combineSuccessText;
    [SF] private Toggle continuousToggle;

    [Header("기타")]
    [SF] private Sprite defaultImage;
    [SF] private Color highColor = new Color32(0xF2, 0xE8, 0xD5, 0xFF);
    [SF] private Color lowColor = new Color32(0xE0, 0x77, 0x5A, 0xFF);
    [SF, Range(0f, 1f)] private float threshold = 0.5f;
    private bool leftFull;
    private bool rightFull;
    private bool? isLow;
    private bool isCompositionMode;
    private bool isContinuous => continuousToggle.isOn;


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

        if (isCompositionMode)
        {
            bool compositionCk = dm.TryGetMaterialData(id, out var mat);

            if (!compositionCk)
            {
                warningTextToken?.Cancel();
                warningTextToken = new();

                PotionWarningTextPopup("포션은 분해할 수 없습니다.",
                    warningTextToken.Token).Forget();

                return;
            }
            else if (mat.ID == InventoryRules.UnknownPowderMaterialId)
            {
                warningTextToken?.Cancel();
                warningTextToken = new();

                PotionWarningTextPopup("미지의 가루는 분해할 수 없습니다.",
                    warningTextToken.Token).Forget();

                return;
            }

            Sprite compositIcon = mat.materialIcon;
            resultSlot.Init(compositIcon, id, instanceId, slot, this);

            int compositMatId = InventoryRules.UnknownPowderMaterialId;

            if (dm.TryGetMaterialData(compositMatId, out var unknownPowderData))
            {
                leftSlot.image.sprite = unknownPowderData.materialIcon;
                leftSlot.id = compositMatId;
            }

            return;
        }

        bool isMaterial = dm.TryGetMaterialData(id, out var material);
        PotionData potionData = null;
        if (!isMaterial && !dm.TryGetPotionData(id, out potionData)) return;

        Sprite icon = isMaterial ? material.materialIcon : potionData.potionIcon;

        if (!leftFull)
        {
            leftSlot.Init(icon, id, instanceId, slot, this);
            leftFull = true;
            slot.SetAlpha();
            SetHighlightMaterial(isMaterial ? id : 0); // 현준 수정 : 짝 재료 강조 (재료일 때만)
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

        rightSlot.Init(icon, id, instanceId, slot, this);
        rightFull = true;
        slot.SetAlpha();
        SetHighlightMaterial(0); // 현준 수정 : 두 칸이 다 찼으니 강조 끄기
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

    /// <summary>
    /// 조합창 상태 초기화 함수
    /// </summary>
    public void Clear()
    {
        leftFull = false;
        rightFull = false;
        leftSlot.Clear(defaultImage);
        rightSlot.Clear(defaultImage);
        resultSlot.Clear(defaultImage);
        SetHighlightMaterial(0); // 현준 수정 : 닫기, 조합 완료, 모드 전환 모두 여기를 거치므로 강조도 여기서 끕니다.
    }

    // ========================= 현준 수정 =============================

    /// <summary>
    /// 짝 재료를 강조할 기준 재료가 바뀌었을 때 알립니다. 0 이면 강조를 끕니다.
    /// 왼쪽 칸에 재료가 올라가면 그 재료 ID, 오른쪽까지 차거나 Clear 되면 0 입니다. (값이 바뀔 때만 발생)
    /// </summary>
    public event Action<int> OnHighlightMaterialChanged;

    /// <summary> 지금 짝 재료를 강조하는 기준 재료 ID 입니다. 0 이면 강조 없음입니다. </summary>
    public int HighlightMaterialId { get; private set; }

    private void SetHighlightMaterial(int materialId)
    {
        if (HighlightMaterialId == materialId) return;

        HighlightMaterialId = materialId;
        OnHighlightMaterialChanged?.Invoke(materialId);
    }

    // =================================================================

    public void PotionCraftBtnCallback(CallbackContext ev)
    {
        if (!isCompositionMode)
            PotionCraft();
        else
            MaterialComposit();
    }

    public void CombineAndCompositBtn()
    {
        if (!isCompositionMode)
            PotionCraft();
        else
            MaterialComposit();
    }

    public void PotionCraft()
    {
        if (leftSlot.id == 0 || rightSlot.id == 0) return;

        PotionCombination(leftSlot.id, rightSlot.id).Forget();
    }
    public void MaterialComposit()
    {
        if (resultSlot.id == 0) return;

        MaterialComposition(resultSlot.id).Forget();
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
        // 요청을 보내는 동안 조합창이 비워질 수 있으니 출처 생성
        Ingredient left = MakeIngredient(leftSlot, leftId);
        Ingredient right = MakeIngredient(rightSlot, rightId);

        int keywordId = PotionCrafter.RollTrapKeywordId();
        InventoryResult result =
            await InventoryNetworkSyncManager.Instance.CraftAsync(left, right, keywordId);

        if (result.Success)
            Debug.Log($"[Craft] 포션 제작 성공 - {result.Outcome.Potion.Data.potionName}");
        else
        {
            warningTextToken?.Cancel();
            warningTextToken = new();

            string reasonText = "";
            switch (result.Reason)
            {
                case InventoryDenyReason.Full:
                    reasonText = "공용 인벤토리 가득 참";

                    PotionWarningTextPopup("공용 인벤토리가 가득 찼습니다.",
                        warningTextToken.Token).Forget();

                    break;
                case InventoryDenyReason.Invalid:
                    reasonText = "조합 오류";
                    PotionWarningTextPopup("이 조합은 불가능합니다.",
                        warningTextToken.Token).Forget();
                    break;
                case InventoryDenyReason.NotEnough:
                    reasonText = "재료가 충분치 않음";
                    PotionWarningTextPopup("조합에 필요한 소재가 부족합니다.",
                        warningTextToken.Token).Forget();
                    break;
                default:
                    warningTextToken?.Cancel();
                    break;
            }

            Debug.Log($"[Craft] 포션 제작 실패 - {reasonText}");
            Clear();
        }

        if (leftId < 20000 || rightId < 20000)
        {
            Clear();
        }
        else if (!isContinuous)
            Clear();
    }

    public async UniTask MaterialComposition(int materialId)
    {

        InventoryResult result =
            await InventoryNetworkSyncManager.Instance.DisassembleAsync(materialId, 1);

        if (result.Success)
            Debug.Log($"[Composit] 분해 성공");
        else
        {
            string reasonText = "";

            warningTextToken?.Cancel();
            warningTextToken = new();

            switch (result.Reason)
            {
                case InventoryDenyReason.Full:
                    reasonText = "공용 인벤토리 가득 참";

                    PotionWarningTextPopup("공용 인벤토리가 가득 찼습니다.",
                        warningTextToken.Token).Forget();

                    break;
                case InventoryDenyReason.Invalid:
                    reasonText = "재료가 아님";
                    PotionWarningTextPopup("재료만 분해할 수 있습니다.",
                        warningTextToken.Token).Forget();
                    break;
                case InventoryDenyReason.NotEnough:
                    reasonText = "재료가 충분치 않음";
                    PotionWarningTextPopup("분해할 재료가 부족합니다.",
                        warningTextToken.Token).Forget();
                    break;
                default:
                    warningTextToken?.Cancel();
                    break;
            }

            Debug.Log($"[Composit] 분해 실패 - {reasonText}");

            Clear();
        }

        if (!isContinuous)
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
        if (type)
            successPer = 100 - PotionCrafter.GetPlayerCraftFailChance();

        successPerText.SetText($"{successPer}%");

        bool low = successPer / 100 <= threshold;

        if (isLow == low) return;

        isLow = low;
        successPerText.color = low ? lowColor : highColor;
    }

    /// <summary>
    /// 분해 <> 조합 기능 전환 함수
    /// </summary>
    public void DecompositionModeActive()
    {
        Clear();
        if (isCompositionMode)
        {
            DecompositionModeReset();
            return;
        }

        compositionBtn.image.sprite = btnPressed;
        combineAndCompositBtnText.SetText(COMPOSIT_TEXT_KR);

        combineArrow.SetActive(false);
        combineSuccessText.SetActive(false);

        compositionArrow.SetActive(true);
        isCompositionMode = true;
    }

    public void DecompositionModeReset()
    {
        compositionBtn.image.sprite = btnNormal;
        combineAndCompositBtnText.SetText(COMBINE_TEXT_KR);

        compositionArrow.SetActive(false);

        combineArrow.SetActive(true);
        combineSuccessText.SetActive(true);

        isCompositionMode = false;
    }

    public void DecompositionBtnHover(GameObject obj)
    {
        obj.SetActive(true);
    }

    public void DecompositionBtnExit(GameObject obj)
    {
        obj.SetActive(false);
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


