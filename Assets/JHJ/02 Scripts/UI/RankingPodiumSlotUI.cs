using TMPro;
using UnityEngine;

public class RankingPodiumSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject card;
    [SerializeField] private TMP_Text membersText;
    [SerializeField] private TMP_Text dayCountText;
    [SerializeField] private TMP_Text timeText;


    public void Show(RankingEntry entry)
    {
        membersText.text = string.Join(", ", entry.Members);
        dayCountText.text = $"{entry.DayCount}일차 클리어";
        timeText.text = TimeFormat.ToPlayTime(entry.PlayTime);
        card.SetActive(true);
    }

    public void Hide()
    {
        card.SetActive(false);
    }
}
