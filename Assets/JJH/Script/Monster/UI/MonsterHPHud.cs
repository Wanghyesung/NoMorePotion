using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class MonsterHPHud : MonoBehaviour
{
    [SF] private Canvas healthCanvas;
    [SF] private CanvasGroup canvasGroup;

    [Header("체력바 수치 관련")]
    [SF] private Image healthBar;
    [SF] private TextMeshProUGUI nowHealthText;
    [SF] private TextMeshProUGUI maxHealthText;

    private void Start()
    {
        healthCanvas.worldCamera = Camera.main;
    }
}
