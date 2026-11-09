using Photon.Pun;
using Photon.Realtime;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// 로비의 방 목록, 방 생성(코드 충돌 재시도), 방 입장, 코드 입장, 퀵매치, 실패 시 로비 재입장.
/// </summary>
/// <remarks>
/// - NetworkFlowManager 전용. 상태(FlowState) 검사·변경과 Photon 콜백 수신은 매니저가 하고,
///   매니저가 콜백을 받은 뒤 이 클래스의 함수를 호출한다.
/// - 이 클래스는 FlowState와 Photon 콜백을 모른다. 요청을 보내고, 실패를 처리하고, 목록을 들고 있는다.
/// - UI는 매니저가 공개한 RoomList를 구독한다 (이 클래스를 직접 쓰지 않음)
/// </remarks>
public class RoomMatchService : IDisposable
{
    private const int MAX_CREATE_RETRY = 5;

    private readonly Action<string> raiseError;
    private readonly RoomListCache roomListCache = new RoomListCache();
    private readonly ReactiveProperty<IReadOnlyList<RoomInfo>> roomList =
        new ReactiveProperty<IReadOnlyList<RoomInfo>>(new List<RoomInfo>());

    // 방 코드가 겹쳐 재시도할 때 같은 옵션을 다시 쓰기 위해 보관
    private int createRetryCount;
    private RoomOptions pendingRoomOptions;


    // 읽기 전용 목록으로 공개 → 구독하는 쪽에서 목록을 수정할 수 없음
    public ReadOnlyReactiveProperty<IReadOnlyList<RoomInfo>> RoomList => roomList;


    /// <param name="raiseError">실패 메시지를 띄울 함수 (매니저의 에러 알림)</param>
    public RoomMatchService(Action<string> raiseError)
    {
        this.raiseError = raiseError;
    }

    //===============================================================

    #region 방 목록

    /// <summary> OnRoomListUpdate에서 받은 변경분을 캐시에 반영하고 UI에 알림 </summary>
    public void ApplyRoomListUpdate(List<RoomInfo> changedRoomList)
    {
        roomListCache.Apply(changedRoomList);
        roomList.Value = roomListCache.ToList();
    }

    /// <summary> 캐시를 비우고 UI에도 빈 목록을 알림 (로비 입장, 연결 끊김 시 호출) </summary>
    public void ClearRoomList()
    {
        roomListCache.Clear();
        roomList.Value = roomListCache.ToList();
    }

    #endregion

    //===============================================================

    #region 방 생성

    /// <summary>
    /// 검사 없이 방 생성 시작 (방 만들기 요청, 퀵매치 실패 후 새 방 생성에서 호출).
    /// 상태 검사는 호출하는 쪽(매니저)이 한다.
    /// </summary>
    public void CreateRoom(string displayName, int maxPlayers, bool isPublic)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = $"{PhotonNetwork.NickName}의 방";

        RoomOptions option = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = isPublic,

            // 방에 저장할 커스텀 속성 : 키 => 값
            CustomRoomProperties = new Hashtable
            {
                { RoomProps.DISPLAY_NAME, displayName }
            },

            // 로비목록에서도 보이게 할 키 목록
            CustomRoomPropertiesForLobby = new[] { RoomProps.DISPLAY_NAME }
        };

        createRetryCount = 0;
        pendingRoomOptions = option;

        TryCreateRoom();
    }

    /// <summary> OnCreateRoomFailed에서 호출. 코드가 겹쳤으면 새 코드로 재시도, 그 외엔 에러 + 로비 재입장 </summary>
    public void HandleCreateRoomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.GameIdAlreadyExists && createRetryCount < MAX_CREATE_RETRY)
        {
            createRetryCount++;
            TryCreateRoom();
            return;
        }

        Debug.LogWarning($"[RoomMatchService] 방 생성 실패 ({returnCode}): {message}");
        raiseError("방을 만들지 못했습니다. 다시 시도해주세요.");
        RejoinLobby();
    }

    private void TryCreateRoom()
    {
        string code = RoomCodeGenerator.Generate();

        // false = 요청을 보내지 못함 → 실패 콜백이 오지 않으므로 직접 처리
        if (!PhotonNetwork.CreateRoom(code, pendingRoomOptions))
        {
            Debug.LogWarning($"[RoomMatchService] 방 생성 요청 실패: {code}");
            raiseError("방을 만들지 못했습니다. 다시 시도해주세요.");
            RejoinLobby();
        }
    }

    #endregion

    //===============================================================

    #region 방 입장

    /// <summary> 검사 없이 방 입장 요청. 상태 검사는 호출하는 쪽(매니저)이 한다. </summary>
    public void JoinRoom(string roomCode)
    {
        if (!PhotonNetwork.JoinRoom(roomCode))
        {
            Debug.LogWarning($"[RoomMatchService] 방 입장 요청 실패: {roomCode}");
            raiseError("방에 입장하지 못했습니다.");
        }
    }

    /// <summary>
    /// 입력한 방 코드를 정리(공백 제거, 대문자)하고 길이를 검사.
    /// 형식이 틀리면 에러를 알리고 false.
    /// </summary>
    public bool TryNormalizeCode(string input, out string code)
    {
        code = (input ?? "").Trim().ToUpperInvariant();

        if (code.Length != RoomCodeGenerator.CODE_LENGTH)
        {
            raiseError("방 코드 6자리를 입력해 주세요.");
            return false;
        }

        return true;
    }

    /// <summary> OnJoinRoomFailed에서 호출. 사유별 메시지를 알리고 로비 재입장 </summary>
    public void HandleJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[RoomMatchService] 방 입장 실패 사유 : {message} , (코드){returnCode}");

        raiseError(GetJoinFailMessage(returnCode));
        RejoinLobby();
    }

    private static string GetJoinFailMessage(short returnCode)
    {
        switch (returnCode)
        {
            case ErrorCode.GameFull:         return "방이 가득 찼습니다.";
            case ErrorCode.GameClosed:       return "이미 게임이 시작된 방입니다.";
            case ErrorCode.GameDoesNotExist: return "방을 찾을 수 없습니다.";
            default:                         return "방에 입장하지 못했습니다.";
        }
    }

    #endregion

    //===============================================================

    #region 퀵매치

    /// <summary> 검사 없이 빈 자리가 있는 공개 방에 무작위 입장 요청. 상태 검사는 매니저가 한다. </summary>
    public void QuickMatch()
    {
        if (!PhotonNetwork.JoinRandomRoom())
        {
            Debug.LogWarning($"[RoomMatchService] 퀵 매치 실패");
            raiseError("빠른 입장에 실패했습니다.");
            RejoinLobby();
        }
    }

    /// <summary> OnJoinRandomFailed에서 호출. 들어갈 방이 없으면 새 공개 방 생성, 그 외엔 에러 + 로비 재입장 </summary>
    public void HandleJoinRandomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.NoRandomMatchFound)
        {
            CreateRoom("", RoomRules.MAX_PLAYERS, true);
            return;
        }

        Debug.LogWarning($"[RoomMatchService] 빠른 입장 실패 ({returnCode}): {message}");
        raiseError("빠른 입장에 실패했습니다.");
        RejoinLobby();
    }

    #endregion

    //===============================================================

    #region 내부

    // 방 입장, 생성 요청 시 PUN이 로비에서 빠지고 실패하면 마스터 서버에만 남음 → 로비 재입장 필요
    private void RejoinLobby()
    {
        if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
            PhotonNetwork.JoinLobby();
    }

    #endregion

    public void Dispose()
    {
        roomList.Dispose();
    }
}
