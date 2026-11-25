using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 클릭 대상의 자식 테두리. 안내 중 지정한 마우스 버튼으로 클릭하면 끈다.
public sealed class GuideClickHighlight : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject border;
    [SerializeField] private bool hideOnClick = true;
    [SerializeField] private UnityEvent clickedEvent = new();
    private PointerEventData.InputButton clickButton;
    private bool keepVisible;
    private Selectable selectable;

    private void Awake() => selectable = GetComponent<Selectable>();

    public void Show(PointerEventData.InputButton button)
    {
        keepVisible = false;
        clickButton = button;
        border.SetActive(true);
    }

    // 네트워크로 아이템 이동이 확정될 때까지 테두리를 유지한다.
    public void ShowUntilHidden()
    {
        keepVisible = true;
        border.SetActive(true);
    }

    public void Hide() => border.SetActive(false);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (keepVisible || !hideOnClick || !border.activeSelf || eventData.button != clickButton)
            return;
        if (selectable != null && !selectable.IsInteractable())
            return;
        Hide();
        clickedEvent.Invoke();
    }
}
