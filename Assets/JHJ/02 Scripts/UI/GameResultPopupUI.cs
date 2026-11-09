using R3;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 판 종료(클리어/패배) 시 결과창 표시. 닫으면 아웃게임으로 나감.
/// GameSessionManager를 구독만 하며, GameSessionManager는 결과창을 모른다.
/// </summary>
/// <remarks>
/// 항상 켜져 있는 오브젝트(캔버스)에 붙이고 popupRoot만 켜고 끈다.
/// popupRoot에 붙이면 꺼져 있는 동안 Start가 불리지 않아 구독이 안 됨
/// </remarks>
public class GameResultPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text rankingText;
    [SerializeField] private Button closeBtn;

    [Header("색상")]
    [SerializeField] private Color clearColor = new Color(0.82f, 0.68f, 0.4f);     // 황동 #D2AE66
    [SerializeField] private Color defeatColor = new Color(0.64f, 0.33f, 0.25f);   // 위험 #A4553F


    private void Awake()
    {
        popupRoot.SetActive(false);
        closeBtn.onClick.AddListener(Close);
    }

    private void Start()
    {
        // GameSessionManager.Instance는 Awake에서 등록되므로 Start에서 구독
        if (GameSessionManager.Instance == null)
        {
            Debug.LogWarning("[GameResultPopupUI] GameSessionManager 없음, 결과창이 뜨지 않음");
            return;
        }

        // Ended 알림 시점엔 Record가 이미 채워져 있음 (GameSessionManager.Finish의 순서 보장)
        // 표시만 하고 TimeManager를 읽거나 랭킹을 등록하지 않음
        GameSessionManager.Instance.Phase
            .Where(r => r == SessionPhase.Ended)
            .Subscribe(r => Show(GameSessionManager.Instance.Record.Value))
            .AddTo(this);

        // 등록 결과는 기록보다 늦게 올 수 있으므로 따로 구독
        GameSessionManager.Instance.RankingSubmit
            .Subscribe(s => rankingText.text = ToRankingMessage(s))
            .AddTo(this);

        // 등록 결과는 기록보다 늦게 올 수 있으므로 따로 구독 (온라인 등록이 비동기가 될 때 대비)
    }


    private void Show(GameRecord record)
    {
        bool isClear = record.Result == GameResult.Clear;

        titleText.text = isClear ? "클리어!" : "패배";
        titleText.color = isClear ? clearColor : defeatColor;
        playTimeText.text = FormatPlayTime(record.PlayTime);
        dayText.SetText("{0}일차", record.DayCount);

        closeBtn.interactable = true;
        popupRoot.SetActive(true);
    }

    private void Close()
    {
        // 씬 전환 전 중복 클릭 방지
        closeBtn.interactable = false;

        NetworkFlowManager manager = NetworkFlowManager.Instance;
        if (manager == null)
        {
            // 인게임 씬을 바로 실행한 경우(에디터 테스트) 매니저가 없음
            Debug.LogWarning("[GameResultPopupUI] NetworkFlowManager 없음, 아웃게임 씬을 직접 불러옴");
            SceneManager.LoadScene(SceneNames.OUT_GAME);
            return;
        }

        manager.RequestLeaveGame();
    }

    // 1시간 미만은 분:초, 이상은 시:분:초
    private static string FormatPlayTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        int h = total / 3600;
        int m = total % 3600 / 60;
        int s = total % 60;

        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    private static string ToRankingMessage(RankingSubmitResult submit)
    {
        switch (submit)
        {
            case RankingSubmitResult.Pending:       return "랭킹 등록 중...";
            case RankingSubmitResult.Submitted:      return "랭킹에 등록되었습니다!";
            case RankingSubmitResult.Offline:        return "오프라인 모드에서는 랭킹에 등록되지 않습니다";
            case RankingSubmitResult.Failed:        return "랭킹 등록에 실패했습니다.";
            case RankingSubmitResult.NotEligible:    return "클리어 시 랭킹에 등록됩니다";
            default:                                 return "";   // None : 등록 결과 대기
        }
    }
}
