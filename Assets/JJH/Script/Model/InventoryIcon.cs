using System;
using Enzeun.Runtime;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using SF = UnityEngine.SerializeField;

public class InventoryIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{

    [SF] private ItemInventory inventory;
    [SF] private CanvasGroup alphaCanvas;
    // 아이템 아이콘
    [SF] private Image itemImage;
    // 아이템 개수 텍스트
    [SF] private TextMeshProUGUI itemCountText;
    [SF] private TextMeshProUGUI itemSlotNum;
    [SF] private Button button;
    public bool isCombinedSlot = false;
    public int slotNum = -1;

    [SF, Range(0f, 1f)] private float emptyAlpha = 0.3f;

    public ItemInstance Item { get; private set; }
    private RectTransform rect;
    public int BoundInstanceId => Item != null ? Item.instanceId : 0;

    public event Action<InventoryIcon> OnClicked;
    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => OnClicked?.Invoke(this));
        rect = GetComponent<RectTransform>();
        SetSlotNum(slotNum);
    }

    public void SetParentInventory(ItemInventory inven)
    {
        inventory = inven;
    }

    /// <summary>
    /// 알파 직접 지정 함수
    /// </summary>
    /// <param name="alpha">지정할 알파 값</param>
    public void SetAlpha(float alpha)
    {
        AlphaCanvas.alpha = alpha;
    }

    // 프리팹에 CanvasGroup 이 연결돼 있지 않은 칸(개인 인벤 칸)도 동작하도록, 없으면 자기 오브젝트에서 찾고 그래도 없으면 만듭니다.
    private CanvasGroup AlphaCanvas
    {
        get
        {
            if (alphaCanvas == null)
            {
                alphaCanvas = GetComponent<CanvasGroup>();

                if (alphaCanvas == null)
                    alphaCanvas = gameObject.AddComponent<CanvasGroup>();
            }

            return alphaCanvas;
        }
    }


    /// <summary>
    /// 알파 간편 조절 함수, 조절값 0.3f
    /// </summary>
    /// <param name="type">false : 0.3, true : 1</param>
    public void SetAlpha(bool type = false)
    {
        if (type)
            AlphaCanvas.alpha = 1;
        else
            AlphaCanvas.alpha = emptyAlpha;
    }

    /// <summary>단축키 번호 표시 (0~8 → 1~9, 9 → 0)</summary>
    public void SetSlotNum(int num)
    {
        slotNum = num;
        if (itemSlotNum == null || num < 0) return;

        itemSlotNum.gameObject.SetActive(true);
        itemSlotNum.text = num + 1 < 10 ? (num + 1).ToString() : "0";
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
        SetAlpha(true);
        Reload();
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
        //Item.itemImage != null << 임시 비활성화 이미지 생기면 넣을것
        bool enabled = Item != null && Item.ItemCount > 0;
        itemImage.enabled = enabled;
        itemCountText.enabled = enabled;
        itemCountText.text = enabled &&
            Item.ItemCount > 1 ? Item.ItemCount.ToString() : string.Empty;


        if (!enabled) return;

        itemImage.sprite = Item.itemImage;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Right)
        {
            if (Item == null || Item.ItemCount <= 0) return;
            
            inventory.potionCombined.BoundSlot(Item.itemId, Item.instanceId, this);
            inventory.potionCombined.RefreshSuccessPerText();
        }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (Item != null) InventoryTooltip.Instance.Show(Item, rect);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (Item != null) InventoryTooltip.Instance.Hide();
    }


    private void OnDestroy()
    {
        if (Item != null) Item.OnChanged -= Reload;
    }


    // ========================= 현준 수정 =============================

    /// <summary>
    /// 개인 인벤토리의 칸인지 여부입니다. 조합 요청에서 출처를 "내 개인 인벤토리의 이 칸"으로 보낼 때 사용합니다. (칸 번호는 slotNum)
    /// </summary>
    public bool isPersonalSlot = false;

    /// <summary>
    /// 개인 인벤토리 칸(InventorySlot)의 내용을 그립니다. 재료, 포션, 빈 칸을 모두 처리합니다.
    /// 개인 인벤토리는 칸이 10개로 고정이라 빈 칸도 알파를 바꾸지 않고 아이콘만 비웁니다. (칸과 퀵슬롯 번호가 그대로 보임)
    /// </summary>
    public void ShowSlot(InventorySlot slot)
    {
        if (slot.IsEmpty)
        {
            Clear();
            return;
        }

        if (slot.Kind == InventorySlotKind.Potion && slot.Potion != null && slot.Potion.Data != null)
        {
            Init(new ItemInstance(slot.Potion.Data.potionID, slot.Potion.Data.potionIcon,
                InventorySlotKind.Potion, 1, slot.Potion.InstanceId));
            return;
        }

        if (slot.Kind == InventorySlotKind.Material)
        {
            Sprite icon = null;
            if (PotionDataManager.Instance != null && PotionDataManager.Instance.TryGetMaterialData(slot.MaterialId, out var material))
                icon = material.materialIcon;

            Init(new ItemInstance(slot.MaterialId, icon, InventorySlotKind.Material, slot.Count));
            return;
        }

        Clear();
    }

    /// <summary>
    /// 조합창에서 빠졌을 때 알파를 원래대로 돌립니다. 개인 칸은 항상 선명하게, 공용 칸은 아이템이 있으면 선명하게 비었으면 흐리게 합니다.
    /// </summary>
    public void RestoreAlpha()
    {
        SetAlpha(isPersonalSlot || (Item != null && Item.ItemCount > 0));
    }


}