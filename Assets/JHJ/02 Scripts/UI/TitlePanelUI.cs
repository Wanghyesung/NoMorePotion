using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

public class TitlePanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button multiBtn;
    [SerializeField] private Button singleBtn;
    [SerializeField] private Button quitBtn;
    [SerializeField] private Button settingsBtn;
    [SerializeField] private SettingsPopupUI settingsPopup;
    [SerializeField] private ConfirmPopupUI confirmPopup;
    [SerializeField] private PopupUI infoPopup;

    private NetworkFlowManager manager;
    private readonly TutorialStore tutorialStore = new TutorialStore();

    private void Start()
    {
        manager = NetworkFlowManager.Instance;

        if(manager == null)
        {
            Debug.LogError("[TitlePanelUI] NetworkFlowManager 없음");
            return;
        }

        settingsBtn.onClick.AddListener(settingsPopup.Open);
        multiBtn.onClick.AddListener(manager.RequestConnectOnline);
        singleBtn.onClick.AddListener(manager.RequestEnterOffline);
        quitBtn.onClick.AddListener( () => QuitFlow().Forget() );

        manager.State                                      // 상태가 바뀔때마다 값을 보냄
            .Select(s => s == FlowState.Title)               // 받은 상태가 Title인지?
            .Subscribe(isTitle =>
            {
                multiBtn.interactable = isTitle;
                singleBtn.interactable = isTitle;

            })   //해당 값을 받아서 interactable에 대입
            .AddTo(this);

        if(!tutorialStore.HasPrompted && manager.CurrentState == FlowState.Title)
            AskTutorialFlow().Forget();
    }

    private async UniTaskVoid AskTutorialFlow()
    {
        bool start = await Confirm("튜토리얼을 진행하시겠습니까?");

        tutorialStore.MarkPrompted();

        if (start)
            manager.RequestStartTutorial();
        else if (infoPopup != null)
            infoPopup.Show("튜토리얼은 설정 > 게임 탭에서 언제든 진행할 수 있습니다.");
    }

    private async UniTaskVoid QuitFlow()
    {
        if (await Confirm("게임을 종료하시겠습니까?"))
            QuitGame.Quit();
    }

    // 확인 팝업이 없으면 바로 진행
    private UniTask<bool> Confirm(string message)
    {
        return confirmPopup != null ? confirmPopup.ShowAsync(message) : UniTask.FromResult(true);
    }

}
