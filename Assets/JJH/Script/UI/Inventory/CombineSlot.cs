using Enzeun.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HIn = UnityEngine.HideInInspector;

public class CombineSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    
    public Image image;
    public RectTransform rect;
    [HIn] public InventoryIcon boundSlot;
    [HIn] public int id;
    [HIn] public int instanceId;

    private CombinationController combined;

    public void Init(Sprite image, int id, int instanceId, InventoryIcon slot, CombinationController combined)
    {
        this.combined = combined;
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
        if (boundSlot != null)
            boundSlot.RestoreAlpha();
        boundSlot = null;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (id == 0) return;

        ItemInstance item = new(id, null, InventorySlotKind.Potion);
        if (item != null) InventoryTooltip.Instance.Show(item, rect);
    }

    public void OnPointerExit(PointerEventData e)
    {
        InventoryTooltip.Instance.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            combined.Clear();
            combined.RefreshSuccessPerText(false);
        }
    }
}
