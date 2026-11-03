using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

// 버튼 클릭 시 MMF 피드백 재생 (버튼 프리팹에 붙여서 사용)
[RequireComponent(typeof(Button))]
public class ButtonFeedback : MonoBehaviour
{
    [SerializeField] private MMF_Player clickFeedback;

    private void Awake()
    {
        if(clickFeedback == null)
            clickFeedback = GetComponent<MMF_Player>();

        if(clickFeedback == null)
        {
            Debug.LogWarning($"[ButtonFeedback] {name}에 MMF_Player 없음");
            return;
        }

        GetComponent<Button>().onClick.AddListener(() => clickFeedback.PlayFeedbacks());
    }
}
