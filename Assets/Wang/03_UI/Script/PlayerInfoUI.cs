using System;
using Enzeun.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 수치는 원본에서 읽고, 이 컴포넌트는 HUD 표시만 담당한다.
public sealed class PlayerInfoUI : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private UISlider healthSlider;
    private int lastHealth;
    private int lastMaxHealth;
    private bool healthInitialized;
    [SerializeField] private BodyTemperatureManager bodyTemperatureManager;
    [SerializeField] private MentalStrengthManager mentalStrengthManager;
    [SerializeField] private HydrationManager hydrationManager;
    [SerializeField] private SatiationManager satiationManager;
    [SerializeField] private Gauge bodyTemperature = new();
    [SerializeField] private Gauge mentalStrength = new();
    [SerializeField] private Gauge hydration = new();
    [SerializeField] private Gauge satiation = new();


    [Serializable]
    private sealed class Gauge
    {
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text valueText;
        private int lastValue = int.MinValue;

        public void Refresh(int value)
        {
            if (lastValue == value)
                return;

            lastValue = value;

            // 생존 수치는 섭씨 온도가 아니라 모두 0~100 게이지다.
            if (fill != null)
                fill.fillAmount = Mathf.Clamp01(value / 100f);
            if (valueText != null)
                valueText.SetText("{0}", value);
        }
    }

    public void BindPlayer(Player target)
    {
        player = target;
        healthInitialized = false;
        RefreshHealth();
    }

    public void UnbindPlayer(Player target)
    {
        // 이전 플레이어가 뒤늦게 파괴돼도 새 플레이어의 연결을 끊지 않는다.
        if (player != target)
            return;

        player = null;
        healthInitialized = false;
    }

    private void RefreshHealth()
    {
        if (player == null || healthSlider == null || player.Character == null)
            return;

        int health = player.Character.HP;
        int maxHealth = player.Character.MaxHP;
        if (healthInitialized == false || lastMaxHealth != maxHealth)
        {
            healthSlider.SetRange(maxHealth, health);
            healthInitialized = true;
        }
        else if (lastHealth != health)
        {
            healthSlider.UpdateSlider(health);
        }

        lastHealth = health;
        lastMaxHealth = maxHealth;
    }

    private void LateUpdate()
    {
        // 현재 원본에는 수치 변경 이벤트가 없어 값만 비교한다.
        // 같은 상태 구간 안의 변화도 반영하며, 검색이나 매 프레임 UI 갱신은 하지 않는다.

        RefreshHealth();

        if (bodyTemperatureManager != null)
            bodyTemperature.Refresh(bodyTemperatureManager.bodyTemperature);
        if (mentalStrengthManager != null)
            mentalStrength.Refresh(mentalStrengthManager.mentalStrength);
        if (hydrationManager != null)
            hydration.Refresh(hydrationManager.hydration);
        if (satiationManager != null)
            satiation.Refresh(satiationManager.satiation);
    }

  
}
