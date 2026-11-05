using TMPro;
using UnityEngine;
using UnityEngine.UI;
using R3;

public class TimeHUDUI : MonoBehaviour
{
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text phaseLabelText;
    [SerializeField] private TMP_Text remainText;
    [SerializeField] private Image phaseIcon;
    [SerializeField] private Sprite daySprite;
    [SerializeField] private Sprite nightSprite;


    private TimeManager timeManager;

    private void Start()
    {
        timeManager = TimeManager.Instance;

        if(timeManager == null)
        {
            Debug.LogWarning("[TimeHUDUI] 타임매니저 없음");
            return;
        }

        timeManager.DayCount.Subscribe(value => dayText.SetText("{0}일차", value)).AddTo(this);
        timeManager.Phase.Subscribe(phase =>
        {
            bool isDay = phase == TimePhase.Day;
            phaseLabelText.text = isDay ? "밤까지" : "낮까지";
            phaseIcon.sprite = isDay ? daySprite : nightSprite;
        })
        .AddTo(this);

        timeManager.RemainSec.Subscribe(sec => remainText.text = $"{sec / 60:00}:{sec % 60:00}")
            .AddTo(this);

    }
}
