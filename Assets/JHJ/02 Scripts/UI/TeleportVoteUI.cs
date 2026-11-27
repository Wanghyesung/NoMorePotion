using Photon.Pun;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class TeleportVoteUI : MonoBehaviour
{
    [SF] private CanvasGroup group;
    [SF] private TMP_Text requestText;
    [SF] private TMP_Text timeText;
    [SF] private TMP_Text countText;
    [SF] private TMP_Text resultText;
    [SF] private Image timerFill;
    [SF] private GameObject[] slots;
    [SF] private GameObject[] slotChecks;
    [SF] private GameObject keyGuide;
    [SF] private GameObject waitingText;
    [SF] private GameObject deadText;
    [SF] private float resultSeconds = 1.5f;


    private int deadline;
    private int totalMs;
    private bool isOpen;
    private float hideAt;       // 결과 표시 후 숨길 시간
    private CastleTeleportManager manager;



    private void Start()
    {
        SetVisible(false);

        manager = CastleTeleportManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning("[TeleportVoteUI] CastleTeleportManager 인스턴스 없음");
            return;
        }

        manager.VoteOpened.Subscribe(OnOpened).AddTo(this);
        manager.VoteProgress.Subscribe(OnProgress).AddTo(this);
        manager.VoteClosed.Subscribe(OnClosed).AddTo(this);
    }

    private void Update()
    {
        if (hideAt > 0 && Time.time >= hideAt)
        {
            SetVisible(false);
            hideAt = 0;
        }

        if (!isOpen)
            return;

        int remaining = Mathf.Max(0, deadline - PhotonNetwork.ServerTimestamp);
        timerFill.fillAmount = (float)remaining / totalMs;
        timeText.text = $"{Mathf.CeilToInt((float)remaining / 1000)}초";

        RefreshFooter();

        if (manager.HasAnswered)
            return;

        if (!manager.IsLocalActive())
            return;

        if (InputManager.Instance == null || !InputManager.Instance.HasGameplayInput)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
            manager.Answer(true);

        else if (Keyboard.current.f2Key.wasPressedThisFrame)
            manager.Answer(false);
    }

    #region 알림처리
    private void OnOpened((string requester, int deadline) data)
    {
        hideAt = 0;
        this.deadline = data.deadline;
        totalMs = Mathf.Max(1, deadline - PhotonNetwork.ServerTimestamp);
        requestText.text = $"{data.requester}님이 마왕성 이동을 요청했어요";
        resultText.gameObject.SetActive(false);
        isOpen = true;
        SetVisible(true);
    }

    private void OnProgress((int agreed, int total) data)
    {
        countText.text = $"{data.agreed}/{data.total}";
        for(int i = 0; i < slots.Length; i++)
        {
            slots[i].SetActive(i < data.total);
            slotChecks[i].SetActive(i < data.agreed);
        }
    }

    private void OnClosed((VoteCloseReason reason, string name) data)
    {
        isOpen = false;
        string msg = ReasonText(data.reason, data.name);
        keyGuide.SetActive(false);
        waitingText.SetActive(false);
        deadText.SetActive(false);

        resultText.text = msg;
        resultText.gameObject.SetActive(true);
        NetworkFlowManager.Instance?.AddSystemChat(msg);
        hideAt = Time.time + resultSeconds;
    }

    private string ReasonText(VoteCloseReason reason, string name)
    {
        string msg = "VoteCloseReason 확인 불가";

        switch (reason)
        {
            case VoteCloseReason.Passed:
                msg = "모두 동의했어요. 마왕성으로 이동해요";
                break;

            case VoteCloseReason.PlayerChanged:
                msg = $"인원이 바뀌어 투표가 취소됐어요";
                break;

            case VoteCloseReason.Rejected:
                msg = $"{name}님이 거절했어요";
                break;

            case VoteCloseReason.TimeOut:
                msg = "응답 시간이 지났어요";
                break;
        }

        return msg;
    }
    #endregion

    #region 메서드
    private void SetVisible(bool visible)
    {
        group.alpha = visible ? 1 : 0;
        group.blocksRaycasts = visible;
    }

    private void RefreshFooter()
    {
        bool answered = manager.HasAnswered;
        bool alive = manager.IsLocalActive();

        waitingText.SetActive(answered);
        deadText.SetActive(!answered && !alive);
        keyGuide.SetActive(!answered && alive);
    }
    #endregion
}
