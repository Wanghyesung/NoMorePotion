using DamageNumbersPro;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using SF = UnityEngine.SerializeField;

public class CopyText : MonoBehaviour
{
    [SF] private DamageNumber saveText;
    [SF] private RectTransform canvasRect;
    [SF] private float copyNoticeCooldown = 1.5f;
    private float nextNoticeTime;

    public void RoomNumberCopyText(TextMeshProUGUI roomText)
    {
        string copyText = roomText.text;

        GUIUtility.systemCopyBuffer = copyText;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Mouse.current.position.ReadValue(),
            null,
            out Vector2 localPoint
        );
        saveText.leftText = "복사되었습니다!";

        if (Time.unscaledTime < nextNoticeTime) return;
        nextNoticeTime = Time.unscaledTime + copyNoticeCooldown;

        saveText.SpawnGUI(canvasRect, localPoint + new Vector2(0, 50));
    }
}
