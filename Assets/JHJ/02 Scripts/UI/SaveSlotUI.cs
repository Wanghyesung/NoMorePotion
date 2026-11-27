using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class SaveSlotUI : MonoBehaviour
{
    [SF] private Toggle toggle;
    [SF] private TMP_Text slotNumberText;
    [SF] private TMP_Text titleText;
    [SF] private TMP_Text detailText;
    [SF] private TMP_Text playTimeText;
    [SF] private TMP_Text dateText;


    private int slot;

    public bool IsSelected => toggle.isOn;

    
    public SaveData Data { get; private set; }
    public bool HasData => Data != null;


    public void Setup(int slot, Action onChanged)
    {
        this.slot = slot;
        slotNumberText.text = $"슬롯 {slot + 1}";

        toggle.onValueChanged.AddListener(_ => onChanged());
    }

    public void Refresh()
    {
        SaveStore.TryLoad(slot, out SaveData loaded);
        Data = loaded;

        if(!HasData)
        {
            titleText.text = "빈 슬롯";
            detailText.text = "";
            playTimeText.text = "플레이 시간 - ";
            dateText.text = "저장 날짜 -";
        }
        else
        {
            titleText.text = $"{Data.Clock.DayCount}일차";
            detailText.text = $"보스 {Data.ClearedBossArea?.Count ?? 0} / 6";
            playTimeText.text = $"플레이 시간  {TimeFormat.ToPlayTime(Data.Clock.TotalElapsed)}";
            dateText.text = $"저장 날짜  {Data.SavedAt:yyyy.MM.dd HH:mm}";
        }
    }

    public void SetSelected(bool selected)
    {
        toggle.SetIsOnWithoutNotify(selected);
    }
}
