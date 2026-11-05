using UnityEngine;
using MoreMountains.Feedbacks;

// MMF_Player의 비네트 효과를 실행하고 Stop 호출 전까지 유지한다.
[DisallowMultipleComponent]
public sealed class VisionEffect : MonoBehaviour
{
    [SerializeField] private MMF_Player feedbacks;
    private bool applied;

    // 피드백의 자동 복구를 끄고 커브의 마지막 값을 유지하도록 설정해야 한다.
    public void Play()
    {
        if (!isActiveAndEnabled || feedbacks == null || applied)
            return;

        applied = true;
        feedbacks.PlayFeedbacks();
    }

    public void Stop()
    {
        if (applied == false)
            return;

        applied = false;
        if (feedbacks != null)
        {
            feedbacks.StopFeedbacks();
            feedbacks.RestoreInitialValues();
        }
    }

    private void OnDisable()
    {
        Stop();
    }
}
