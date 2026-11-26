using Cysharp.Threading.Tasks;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RespawnCountdownUI : MonoBehaviour
{
    [SerializeField, Min(0f)] private float waitSeconds = 5f;
    [SerializeField] private Image timerFill;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Button respawnButton;

    private int deathAt;
    private int displayVersion;
    private bool ready;
    private int DurationMs => Mathf.RoundToInt(waitSeconds * 1000f);

    // 사망 시각과 현재 Photon 시각으로 부활까지 남은 밀리초를 계산한다.
    internal int RemainingMilliseconds(int start, int now)
    {
        int elapsed = now - start;
        return Mathf.Clamp(DurationMs - elapsed, 0, DurationMs);
    }

    // 이번 사망 시각을 설정하고 승인 대기 상태로 팝업을 연다.
    public void Show(int deathTimestamp)
    {
        deathAt = deathTimestamp;
        ready = false;
        gameObject.SetActive(true);
    }

    // 팝업용 커서를 표시하고 방장 승인을 기다리는 동안 카운트다운을 그린다.
    private void OnEnable()
    {
        if (InputManager.Instance != null)
            InputManager.Instance.SetRespawnCursor(true);

        respawnButton.interactable = ready;
        if (!ready)
            DrawCountdown(++displayVersion).Forget();
    }

    // 팝업을 닫으면 기존 카운트다운 갱신을 끝내고 커서 표시 요청을 해제한다.
    private void OnDisable()
    {
        displayVersion++;
        if (InputManager.Instance != null)
            InputManager.Instance.SetRespawnCursor(false);
    }

    // 게이지와 남은 초를 갱신하다가 마지막 0.1초에서 방장 승인을 기다린다.
    private async UniTask DrawCountdown(int version)
    {
        int lastSecond = -1;
        while (displayVersion == version && !ready)
        {
            int remaining = RemainingMilliseconds(deathAt, PhotonNetwork.ServerTimestamp);
            int displayMs = Mathf.Clamp(remaining, Mathf.Min(100, DurationMs), DurationMs);
            timerFill.fillAmount = DurationMs > 0 ? (float)displayMs / DurationMs : 0f;
            int second = Mathf.CeilToInt(displayMs / 1000f);
            if (second != lastSecond)
            {
                timerText.SetText("{0}", second);
                lastSecond = second;
            }

            if (remaining <= 100)
                return;

            await UniTask.NextFrame(destroyCancellationToken);
        }
    }

    // 방장 승인을 받으면 타이머를 0으로 표시하고 부활 버튼을 활성화한다.
    public void SetReady()
    {
        ready = true;
        timerFill.fillAmount = 0f;
        timerText.SetText("0");
        respawnButton.interactable = true;
    }
}
