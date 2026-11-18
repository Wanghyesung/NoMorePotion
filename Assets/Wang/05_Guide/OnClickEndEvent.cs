using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class OnClickEndEvent : GuideEndEvent, IPointerClickHandler
{
    private Selectable selectable;

    private void Awake() => selectable = GetComponent<Selectable>();

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (selectable != null && !selectable.IsInteractable())
            return;
        Complete();
    }
}
