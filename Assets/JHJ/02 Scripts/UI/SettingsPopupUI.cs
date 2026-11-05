using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPopupUI : MonoBehaviour
{
    [SerializeField] private Button toTitleBtn;
    [SerializeField] private Button quitBtn;
    [SerializeField] private Button closeBtn;
    [SerializeField] private ConfirmPopupUI confirmPopup;

    private void Awake()
    {
        closeBtn.onClick.AddListener(Close);
        toTitleBtn.onClick.AddListener(() => ToTitleFlow().Forget());
        quitBtn.onClick.AddListener(() => QuitFlow().Forget());
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private async UniTaskVoid ToTitleFlow()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;
        if (manager == null)
        {
            Debug.LogError("[SettingsPopupUI] NetworkFlowManager 없음");
            return;
        }

        if (!await Confirm("타이틀로 돌아가시겠습니까?"))
            return;

        Close();
        manager.RequestReturnToTitle();
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
