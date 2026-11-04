using Cysharp.Threading.Tasks;
using Photon.Realtime;
using R3;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPanelUI : MonoBehaviour
{
    [Header("방 목록")]
    [SerializeField] private Transform content;
    [SerializeField] private RoomRowUI roomRowPrefab;

    [Header("TEXT")]
    [SerializeField] private TMP_Text roomCountText;
    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_InputField codeInput;

    [Header("Buttons")]
    [SerializeField] private Button joinByCodeBtn;
    [SerializeField] private Button createBtn;
    [SerializeField] private Button quickMatchBtn;
    [SerializeField] private Button quitBtn;
    [SerializeField] private Button nicknameEditBtn;

    [Header("popup")]
    [SerializeField] private CreateRoomPopupUI createPopup;
    [SerializeField] private NicknamePopupUI changeNickPopup;
    [SerializeField] private ConfirmPopupUI quitConfirmPopup;


    private NetworkFlowManager manager;

    private void Awake()
    {
        manager = NetworkFlowManager.Instance;
    }

    private void Start()
    {
        if (manager == null)
            manager = NetworkFlowManager.Instance;   // Awake 때 매니저가 아직 없었을 경우 대비

        if (manager == null)
        {
            Debug.LogError("[LobbyPanelUI] NetworkFlowManager 없음");
            return;
        }

        manager.RoomList.Subscribe(RenderRoomList).AddTo(this);
        manager.Nickname.Subscribe(nick => nicknameText.text = nick).AddTo(this);

        nicknameEditBtn.onClick.AddListener(() => changeNickPopup.Open(manager.Nickname.CurrentValue, true));
        joinByCodeBtn.onClick.AddListener(() => manager.RequestJoinByCode(codeInput.text));
        quickMatchBtn.onClick.AddListener(() => manager.RequestQuickMatch());
        createBtn.onClick.AddListener(() => createPopup.Open());
        quitBtn.onClick.AddListener(() => QuitFlow().Forget());
    }

    private void OnEnable()
    {
        if (manager != null && !manager.HasSavedNickname)
            changeNickPopup.Open(manager.Nickname.CurrentValue, false);
    }

    private void RenderRoomList(List<RoomInfo> rooms)
    {
        // 기존 줄 삭제
        foreach (Transform child in content)
            Destroy(child.gameObject);

        // 새 줄 생성
        foreach(RoomInfo room in rooms)
        {
            RoomRowUI row = Instantiate(roomRowPrefab, content);
            row.Bind(room, manager.RequestJoinRoom);
        }

        // 개수표시
        roomCountText.text = $"방 목록 {rooms.Count}개";
    }

    private async UniTaskVoid QuitFlow()
    {
        bool yes = await quitConfirmPopup.ShowAsync("게임을 종료하시겠습니까?");

        if (yes)
            QuitGame.Quit();
    }
}
