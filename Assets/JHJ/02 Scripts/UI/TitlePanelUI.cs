using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitlePanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button startButton;

    private NetworkFlowManager manager;
    private string defaultNickname;

    private void Start()
    {
        manager = NetworkFlowManager.Instance;

        if(manager == null)
        {
            Debug.LogError("[TitlePanelUI] NetworkFlowManager 없음");
            return;
        }

        defaultNickname = "Player" + Random.Range(1000, 10000);
        string savedNickname = manager.GetSavedNickname();

        nicknameInput.text = string.IsNullOrEmpty(savedNickname)? defaultNickname : savedNickname;

        startButton.onClick.AddListener(OnClickStart);

        manager.State                                      // 상태가 바뀔때마다 값을 보냄
            .Select(s => s == FlowState.Title)               // 받은 상태가 Title인지?
            .Subscribe(isTitle => startButton.interactable = isTitle)   //해당 값을 받아서 interactable에 대입
            .AddTo(this);
    }

    private void OnClickStart()
    {
        string nickname = nicknameInput.text.Trim();
        
        if(string.IsNullOrEmpty(nickname))
            nickname = defaultNickname;

        // UniTask라서 붙임, 결과를 기다리지않고 실행만
        manager.RequestConnect(nickname).Forget();
    }

}
