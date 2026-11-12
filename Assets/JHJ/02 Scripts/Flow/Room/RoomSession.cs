using Photon.Pun;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

/// <summary>
/// 방 안의 플레이어 목록, 레디, 시작 가능 여부 관리.
/// </summary>
/// <remarks>
/// - NetworkFlowManager 전용. 상태(FlowState) 검사·변경과 Photon 콜백 수신은 매니저가 하고,
///   매니저가 콜백을 받은 뒤 이 클래스의 함수를 호출한다.
/// - 이 클래스는 FlowState와 Photon 콜백을 모른다. 값만 들고, 시키는 일만 한다.
/// - UI는 매니저가 공개한 Players / CanStart를 구독한다 (이 클래스를 직접 쓰지 않음)
/// </remarks>
public class RoomSession : IDisposable
{
    private readonly ReactiveProperty<IReadOnlyList<PhotonPlayer>> players =
        new ReactiveProperty<IReadOnlyList<PhotonPlayer>>(new List<PhotonPlayer>());

    private readonly ReactiveProperty<bool> canStart = new ReactiveProperty<bool>(false);


    // 읽기 전용 목록으로 공개 → 구독하는 쪽에서 목록을 수정할 수 없음
    public ReadOnlyReactiveProperty<IReadOnlyList<PhotonPlayer>> Players => players;
    public ReadOnlyReactiveProperty<bool> CanStart => canStart;


    /// <summary>
    /// 현재 방의 플레이어 목록과 시작 가능 여부를 다시 계산해 알림.
    /// 입장, 플레이어 입·퇴장, 방장 변경, 플레이어 속성 변경 시 호출.
    /// </summary>
    public void Refresh()
    {
        // ReactiveProperty는 같은 객체를 다시 넣으면 알리지 않으므로 항상 새 리스트
        players.Value = new List<PhotonPlayer>(PhotonNetwork.PlayerList);
        canStart.Value = CheckCanStart();
    }

    /// <summary> 방을 나가거나 연결이 끊겼을 때 빈 목록으로 초기화 </summary>
    public void Clear()
    {
        players.Value = new List<PhotonPlayer>();
        canStart.Value = false;     // 방을 나간 뒤 이전 방의 시작 가능 값이 남지 않도록
    }

    /// <summary> 방장이고, 방장을 뺀 전원이 레디면 true (방장 혼자여도 true) </summary>
    public bool CheckCanStart()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        foreach (PhotonPlayer player in PhotonNetwork.PlayerList)
        {
            if (player.IsMasterClient)
                continue;

            if (!PlayerProps.IsReady(player))
                return false;
        }

        return true;
    }

    /// <summary> 내 레디 상태를 반대로 바꿈. 방장은 레디 대상이 아니므로 무시 </summary>
    public void ToggleReady()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[RoomSession] 준비 무시됨: 현재 방장");
            return;
        }

        bool isReady = PlayerProps.IsReady(PhotonNetwork.LocalPlayer);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PlayerProps.READY, !isReady } });
    }

    /// <summary>
    /// 방 범위 플레이어 속성(PlayerProps.CreateRoomScopedReset)을 초기화. 방 퇴장·연결 끊김 시 호출
    /// (로컬 플레이어 속성은 다음 방 입장 시 그대로 전송되므로 초기화 필요. 방 밖에서는 로컬 값만 바뀜)
    /// </summary>
    public void ResetRoomScopedProps()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(PlayerProps.CreateRoomScopedReset());
    }

    public void Dispose()
    {
        players.Dispose();
        canStart.Dispose();
    }
}
