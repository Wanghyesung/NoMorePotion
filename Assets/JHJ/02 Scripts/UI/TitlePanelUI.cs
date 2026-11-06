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
    [SerializeField] private ConfirmPopupUI confirmPopup;

    private NetworkFlowManager manager;

    private void Start()
    {
        manager = NetworkFlowManager.Instance;

        if(manager == null)
        {
            Debug.LogError("[TitlePanelUI] NetworkFlowManager 없음");
            return;
        }

        multiBtn.onClick.AddListener(OnClickMulti);
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
    }

    private void OnClickMulti()
    {
        // UniTask라서 붙임, 결과를 기다리지않고 실행만
        manager.RequestConnectOnline().Forget();
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
