using TMPro;
using UnityEngine;

public class RankingRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text membersText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text timeText;

    public void Show(int rank, RankingEntry entry)
    {

        rankText.text = rank.ToString();
        membersText.text = string.Join(", ", entry.Members);
        dayText.text = $"{entry.DayCount}일차";
        timeText.text = TimeFormat.ToPlayTime(entry.PlayTime);

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
