using TMPro;
using UnityEngine;
using UnityEngine.UI;
using R3;

/// <summary>
/// 인게임 HUD 시간 표시: 일차, 다음 단계까지 남은 시간, 원형 시계.
/// TimeManager를 구독만 하며, TimeManager는 HUD를 모른다.
/// </summary>
public class TimeHUDUI : MonoBehaviour
{
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text phaseLabelText;
    [SerializeField] private TMP_Text remainText;

    [Header("원형 시계")]
    [SerializeField] private Image clockFill;          // 깎이는 원 (Filled, Radial 360)
    [SerializeField] private Image clockBackground;    // 뒤 배경 원 (없으면 비워둬도 됨)
    [SerializeField] private Color dayColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color nightColor = new Color(0.15f, 0.2f, 0.45f);

    private TimeManager timeManager;

    private void Start()
    {
        timeManager = TimeManager.Instance;
        if (timeManager == null)
        {
            Debug.LogWarning("[TimeHUDUI] 타임매니저 없음");
            return;
        }

        timeManager.DayCount.Subscribe(day => dayText.SetText("{0}일차", day)).AddTo(this);

        timeManager.Phase.Subscribe(phase =>
        {
            bool isDay = phase == TimePhase.Day;
            phaseLabelText.text = isDay ? "밤까지" : "낮까지";
            clockFill.color = isDay ? dayColor : nightColor;

            // 배경은 다음 단계 색 → 깎일수록 다음 단계가 드러남
            if (clockBackground != null)
                clockBackground.color = isDay ? nightColor : dayColor;
        }).AddTo(this);

        // 분:초 형식. 1초에 한 번만 호출되므로 문자열 생성 부담 없음
        timeManager.RemainSec.Subscribe(sec =>
            remainText.text = $"{sec / 60:00}:{sec % 60:00}"
        ).AddTo(this);
    }

    private void Update()
    {
        if (timeManager == null) return;

        // 꽉 찬 상태에서 깎여가도록 1 - 진행 비율
        clockFill.fillAmount = 1f - timeManager.PhaseProgress;
    }
}