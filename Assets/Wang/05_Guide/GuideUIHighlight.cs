using UnityEngine;
using UnityEngine.EventSystems;

// 대상에 미리 만든 테두리를 켠다. 목록만 지정하면 그 안의 슬롯들을 강조한다.
public sealed class GuideUIHighlight : MonoBehaviour
{
    private GuideClickHighlight[] currentHighlights;

    public void Show(GameObject target) => Show(target, PointerEventData.InputButton.Left);
    public void ShowRightClick(GameObject target) => Show(target, PointerEventData.InputButton.Right);

    private void Show(GameObject target, PointerEventData.InputButton button)
    {
        Hide();
        GuideClickHighlight panel = target.GetComponent<GuideClickHighlight>();
        currentHighlights = panel != null
            ? new[] { panel }
            : target.GetComponentsInChildren<GuideClickHighlight>(true);
        foreach (GuideClickHighlight item in currentHighlights)
            item.Show(button);
    }

    public void Hide()
    {
        if (currentHighlights == null)
            return;
        foreach (GuideClickHighlight item in currentHighlights)
            if (item != null)
                item.Hide();
        currentHighlights = null;
    }
    private void OnDisable() => Hide();
}
