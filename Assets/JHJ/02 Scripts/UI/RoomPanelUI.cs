using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PhotonPlayer = Photon.Realtime.Player;

public class RoomPanelUI : MonoBehaviour
{
    [Header("Room Info")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text playerCountText;

    [Header("Slots")]
    [SerializeField] private Transform slotParent;       // PlayerList (Horizontal Layout Group)
    [SerializeField] private PlayerSlotUI slotPrefab;    // Project 창의 프리팹 에셋

    [Header("Buttons")]
    [SerializeField] private Button leaveBtn;
    [SerializeField] private Button readyBtn;
    [SerializeField] private Button startBtn;

    [SerializeField] private TMP_Text readyBtnText;

    private NetworkFlowManager manager;
    private readonly List<PlayerSlotUI> slots = new List<PlayerSlotUI>();

    private void Awake()
    {
        manager = NetworkFlowManager.Instance;
    }

    private void Start()
    {
        // Awake 시점에 매니저가 아직 없었을 경우 대비
        if (manager == null)
            manager = NetworkFlowManager.Instance;

        if (manager == null)
        {
            Debug.LogError("[RoomPanelUI] NetworkFlowManager 없음");
            return;
        }

        CreateSlots();

        // 슬롯을 만든 뒤에 구독해야 첫 그리기 때 슬롯이 존재함
        manager.Players.Subscribe(RenderSlots).AddTo(this);

        leaveBtn.onClick.AddListener(manager.RequestLeaveRoom);
        readyBtn.onClick.AddListener(manager.RequestToggleReady);
        startBtn.onClick.AddListener(() =>Debug.Log("게임 시작")); // TODO: 게임 시작 연결
        manager.CanStart.Subscribe(can => startBtn.interactable = can).AddTo(this);
    }

    // 방에 들어올 때마다 방 정보 갱신
    private void OnEnable()
    {
        Room room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        roomNameText.text = RoomProps.GetDisplayName(room);
        roomCodeText.text = room.Name;
    }

    // 최대 인원만큼 슬롯을 한 번만 생성
    private void CreateSlots()
    {
        for (int i = 0; i < RoomRules.MAX_PLAYERS; i++)
        {
            PlayerSlotUI slot = Instantiate(slotPrefab, slotParent);
            slots.Add(slot);
        }
    }

    private void RenderSlots(List<PhotonPlayer> players)
    {
        // 방에서 나간 직후(빈 목록 알림)에는 CurrentRoom이 null일 수 있음
        Room room = PhotonNetwork.CurrentRoom;
        if (room == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < players.Count)
                slots[i].ShowPlayer(players[i]);
            else if (i < room.MaxPlayers)
                slots[i].ShowWaiting();
            else
                slots[i].ShowClosed();
        }

        playerCountText.text = $"{players.Count}/{room.MaxPlayers}";

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        PhotonPlayer me = PhotonNetwork.LocalPlayer;

        // 방장이면 시작 버튼, 아니면 레디 버튼 (같은 자리에 하나만 보임)
        startBtn.gameObject.SetActive(me.IsMasterClient);
        readyBtn.gameObject.SetActive(!me.IsMasterClient);

        readyBtnText.text = PlayerProps.IsReady(me) ? "준비 취소" : "준비";
    }
}