using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using SF = UnityEngine.SerializeField;

public class InventoryIcon : MonoBehaviour, IPointerClickHandler
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

    public int BoundInstanceId => Item != null ? Item.instanceId : 0;

    public event Action<InventoryIcon> OnClicked;
    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => OnClicked?.Invoke(this));

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
        alphaCanvas.alpha = alpha;
    }


    /// <summary>
    /// 알파 간편 조절 함수, 조절값 0.3f
    /// </summary>
    /// <param name="type">false : 0.3, true : 1</param>
    public void SetAlpha(bool type = false)
    {
        if (type)
            alphaCanvas.alpha = 1;
        else
            alphaCanvas.alpha = emptyAlpha;
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
        Debug.Log(enabled);
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
            SetAlpha();
            inventory.potionCombined.BoundSlot(Item.itemId, Item.instanceId, this);
        }
        else if (isCombinedSlot)
        {
            inventory.potionCombined.Clear();
        }


    }

    private void OnDestroy()
    {
        if (Item != null) Item.OnChanged -= Reload;
    }
}
;