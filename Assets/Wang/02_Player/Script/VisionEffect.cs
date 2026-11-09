using UnityEngine;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;

// MMF_Player의 비네트 효과를 실행하고 Stop 호출 전까지 유지한다.
[DisallowMultipleComponent]
public sealed class VisionEffect : MonoBehaviour
{
    [SerializeField] private MMF_Player feedbacks;
    private bool applied;
    private MMF_Vignette_URP vignetteFeedback;
    private float defaultIntensity;
    private float currentIntensity;

    private void Awake()
    {
        if (feedbacks == null)
            return;

        vignetteFeedback = feedbacks.GetFeedbackOfType<MMF_Vignette_URP>();
        if (vignetteFeedback != null)
            defaultIntensity = vignetteFeedback.RemapIntensityOne;
    }

    // 피드백의 자동 복구를 끄고 커브의 마지막 값을 유지하도록 설정해야 한다.
    public void Play()
    {
        Play(defaultIntensity);
    }

    // 축소 강도가 바뀌면 원래 값으로 복구한 후 새 강도로 재생한다.
    public void Play(float intensity)
    {
        if (!isActiveAndEnabled || feedbacks == null || vignetteFeedback == null)
            return;

        intensity = Mathf.Clamp01(intensity);
        if (intensity <= 0f)
        {
            Stop();
            return;
        }
        // 다른 버프 이벤트로 같은 값이 전달돼도 비네트 커브를 처음부터 재생하지 않는다.
        if (applied && Mathf.Approximately(currentIntensity, intensity))
            return;

        Stop();
        vignetteFeedback.RemapIntensityOne = intensity;
        currentIntensity = intensity;
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
            // 유지형 피드백은 재생만 멈춰도 마지막 값이 남으므로 초기값 복원을 별도로 호출한다.
            feedbacks.RestoreInitialValues();
        }
        if (vignetteFeedback != null)
            vignetteFeedback.RemapIntensityOne = defaultIntensity;
    }

    private void OnDisable()
    {
        Stop();
    }
}
