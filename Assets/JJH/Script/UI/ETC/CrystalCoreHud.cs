using DG.Tweening;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class CrystalCoreHud : MonoBehaviour
{
    [SF] private TextMeshProUGUI maxHealthText;
    [SF]private TextMeshProUGUI nowHealthText;
    [SF]private Image healthBar;
    [SF] private float healDuration = 0.2f;

    private int maxHealth;
    private float currentHealth;
    public float Ratio => currentHealth / maxHealth;

    private Tween hpTween;

    public void ResetHealth(int hp)
    {
        maxHealth = hp;
        currentHealth = hp;
        UpdateTexts();
    }

    public void HealthChange(int before, int now) => SetHealth(now);

    public void SetHealth(float newHealth)
    {
        newHealth = Mathf.Clamp(newHealth, 0f, maxHealth);
        if (Mathf.Approximately(newHealth, currentHealth)) return;

        bool damaged = newHealth < currentHealth;
        currentHealth = newHealth;
        float ratio = Ratio;

        UpdateTexts();

        hpTween?.Kill();
        healthBar.fillAmount = ratio;
        hpTween = healthBar.DOFillAmount(ratio, healDuration)
        .SetEase(Ease.OutQuad)
        .SetLink(gameObject);
    }

    /// <summary>
    /// 체력 텍스트 업데이트 함수
    /// </summary>
    private void UpdateTexts()
    {
        if (nowHealthText != null)
            nowHealthText.SetText("{0:0}", Mathf.Ceil(currentHealth));

        if (maxHealthText != null)
            maxHealthText.SetText("{0:0}", Mathf.Ceil(maxHealth));
    }
}
