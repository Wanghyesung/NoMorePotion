using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

public class RoomChatService : IDisposable
{
    private const int MAX_LENGTH = 100;
    private const int MAX_HISTORY = 50;
    private const float SEND_COOLDOWN = 0.5f;
    
    private readonly List<ChatMessage> history = new List<ChatMessage>();
    private readonly Subject<ChatMessage> received = new Subject<ChatMessage>();
    private float lastSendTime;


    public IReadOnlyList<ChatMessage> History => history;
    public Observable<ChatMessage> Received => received;

    
    public void Send(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = Truncate(text.Trim());

        if (lastSendTime + SEND_COOLDOWN > Time.time)
        {
            AddSystem("너무 빠르게 보내고 있어요");
            return;
        }

        lastSendTime = Time.time;

        PhotonNetwork.RaiseEvent(
            NetEventCodes.RoomChat,
            text,
            new RaiseEventOptions { Receivers = ReceiverGroup.All },
            SendOptions.SendReliable);
    }

    public void Receive(int senderActor, object content)
    {
        if(content is not string text)
        {
            Debug.LogWarning("[RoomChatService] 메세지 수신 불가, content가 string이 아님");
            return;
        }

        text = Truncate(text);

        Room room = PhotonNetwork.CurrentRoom;

        if (room == null || !room.Players.TryGetValue(senderActor, out PhotonPlayer player))
            return;

        bool isMine = senderActor == PhotonNetwork.LocalPlayer.ActorNumber;
        Add(ChatMessage.CreateUser(player.NickName, text, isMine));
    }

    public void Clear()
    {
        history.Clear();
    }

    public void AddSystem(string text)
    {
        Add(ChatMessage.CreateSystem(text));
    }

    private void Add(ChatMessage msg)
    {
        history.Add(msg);
        if (history.Count > MAX_HISTORY)
            history.RemoveAt(0);
        received.OnNext(msg);
    }

    private static string Truncate(string text)
    {
        return text.Length > MAX_LENGTH ? text.Substring(0, MAX_LENGTH) : text;
    }

    #region 인터페이스
    public void Dispose()
    {
        received.Dispose();
    }
    #endregion
}
