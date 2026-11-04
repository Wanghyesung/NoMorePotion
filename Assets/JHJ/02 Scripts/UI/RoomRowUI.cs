using Photon.Realtime;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button joinBtn;

    private const string WAITING_TEXT = "대기 중";
    private const string PLAYING_TEXT = "게임 중";
    private const string FULL_TEXT = "가득 참";


    public void Bind(RoomInfo room, Action<string> joinCallback)
    {
        nameText.text = RoomProps.GetDisplayName(room);
        codeText.text = room.Name;
        playerCountText.text = $"{room.PlayerCount}/{room.MaxPlayers}";
        
        bool canJoin = room.IsOpen && room.PlayerCount < room.MaxPlayers;
        statusText.text = canJoin ? WAITING_TEXT : (room.IsOpen ? FULL_TEXT : PLAYING_TEXT);

        joinBtn.interactable = canJoin;
        joinBtn.onClick.RemoveAllListeners();
        joinBtn.onClick.AddListener(() => joinCallback(room.Name));

    }

}
