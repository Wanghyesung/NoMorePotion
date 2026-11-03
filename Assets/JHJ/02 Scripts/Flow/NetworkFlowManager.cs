using Cysharp.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class NetworkFlowManager : MonoBehaviourPunCallbacks
{
    private const int MAX_CREATE_RETRY = 5;
    public static NetworkFlowManager Instance { get; private set; }


    private const string NicknameKey = "Nickname";
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(10);


    private ReactiveProperty<FlowState> state;
    private ReactiveProperty<List<RoomInfo>> roomList;
    private Subject<string> error;
    private FirebaseAuthService authService = new FirebaseAuthService();
    private RoomListCache roomListCache = new RoomListCache();
    private int createRetryCount;
    private RoomOptions pendingRoomOptions;

    public ReadOnlyReactiveProperty<FlowState> State => state;
    public ReadOnlyReactiveProperty<List<RoomInfo>> RoomList => roomList;
    public Observable<string> Error => error;
    public FlowState CurrentState => state.Value;

    //===============================================================

    #region LifeCycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Master 씬 로드시 전원이 같은 씬으로 이동
        PhotonNetwork.AutomaticallySyncScene = true;

        // 상태 : 구독하는 순간 현재 값 전달, 같은 값으로 바꾸면 알림X
        state = new ReactiveProperty<FlowState>(FlowState.Title);

        // 에러: 구독 이후에 발생한 메세지만 전달
        error = new Subject<string>();

        roomList = new ReactiveProperty<List<RoomInfo>>(new List<RoomInfo>());

        // 상태 변경 로그 (구독권을 매니저 수명에 묶음 → 매니저 파괴 시 자동 해제)
        state.Subscribe(s => Debug.Log($"[NetworkFlowManager] 상태 변경: {s}")).AddTo(this);

    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        // 매니저가 직접 만든 소스 정리
        // ?. : 중복 매니저는 Awake에서 return해서 null이므로 건너뜀
        state?.Dispose();
        error?.Dispose();
        roomList?.Dispose();
    }
    #endregion

    //===============================================================

    #region State

    private void SetState(FlowState newState)
    {
        state.Value = newState;
    }

    private void RaiseError(string message)
    {
        error.OnNext(message);
    }

    #endregion

    //===============================================================

    #region Connect

    #region 요청
    // 저장된 닉네임 가져오기
    public string GetSavedNickname()
    {
        return PlayerPrefs.GetString(NicknameKey, "");
    }

    public async UniTaskVoid RequestConnect(string nickname)
    {
        if (CurrentState != FlowState.Title) return;

        SetState(FlowState.Connecting);

        PlayerPrefs.SetString(NicknameKey, nickname);
        PlayerPrefs.Save();
        PhotonNetwork.NickName = nickname;

        try
        {
            await authService.SignInAsync().Timeout(LoginTimeout);
        }
        catch (TimeoutException e)
        {
            Debug.LogException(e);
            RaiseError("Login timed out");
            SetState(FlowState.Title);
            return;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            RaiseError("Login failed");
            SetState(FlowState.Title);
            return;
        }

        Debug.Log($"[NetworkFlowManager] Firebase UID: {authService.UserId}");

        if (!PhotonNetwork.ConnectUsingSettings())   // true = 접속 시도를 시작, false = 시도조차 실패
        {
            RaiseError("Failed to connect to server");
            SetState(FlowState.Title);
        }
    }
    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        ClearRoomList();
        SetState(FlowState.Lobby);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        ClearRoomList();

        SetState(FlowState.Title);
        RaiseError($"Disconnected from server. ({cause})");
    }

    #endregion

    #endregion

    //===============================================================

    #region Lobby

    #region 요청

    public void RequestCreateRoom(string displayName, int maxPlayers, bool isPublic)
    {
        // 로비 상태가 아니거나 펀 상태가 로비가 아니면
        if (CurrentState != FlowState.Lobby || PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
            return;

        CreateRoomInternal(displayName, maxPlayers, isPublic);
    }

    // 검사 없이 방 생성 시작 (방 만들기 요청, 퀵매치 실패 후 새 방 생성에서 호출)
    private void CreateRoomInternal(string displayName, int maxPlayers, bool isPublic)
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
            { RoomProps.DisplayName, displayName }
        },

            // 로비목록에서도 보이게 할 키 목록
            CustomRoomPropertiesForLobby = new[] { RoomProps.DisplayName }
        };

        createRetryCount = 0;
        pendingRoomOptions = option;

        TryCreateRoom();
    }

    private void TryCreateRoom()
    {
        string code = RoomCodeGenerator.Generate();

        // false = 요청을 보내지 못함 → 실패 콜백이 오지 않으므로 직접 처리
        if (!PhotonNetwork.CreateRoom(code, pendingRoomOptions))
        {
            Debug.LogWarning($"[NetworkFlowManager] 방 생성 요청 실패: {code}");
            RaiseError("방을 만들지 못했습니다. 다시 시도해주세요.");
        }
    }

    public void RequestJoinRoom(string roomCode)
    {
        if (CurrentState != FlowState.Lobby || PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
            return;

        if (!PhotonNetwork.JoinRoom(roomCode))
        {
            Debug.LogWarning($"[NetworkFlowManager] 방 입장 요청 실패: {roomCode}");
            RaiseError("방에 입장하지 못했습니다.");
        }
    }

    public void RequestJoinByCode(string roomCode)
    {
        string code = (roomCode ?? "").Trim().ToUpperInvariant();

        // TODO const 모음 정리해서 수정
        if (code.Length != 6)
        {
            RaiseError("방 코드 6자리를 입력해 주세요.");
            return;
        }

        RequestJoinRoom(code);
    }

    public void RequestQuickMatch()
    {
        if (CurrentState != FlowState.Lobby || PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
        {
            Debug.Log($"[NetworkFlowManager] 퀵매치 무시됨 - 상태: {CurrentState}, PUN: {PhotonNetwork.NetworkClientState}");
            return;
        }

        if (!PhotonNetwork.JoinRandomRoom())
        {
            Debug.LogWarning($"[NetworkFlowManager] 퀵 매치 실패");
            RaiseError("빠른 입장에 실패했습니다.");
        }
    }

    #endregion

    #region Photon Callbacks
    public override void OnRoomListUpdate(List<RoomInfo> changedRoomList)
    {
        roomListCache.Apply(changedRoomList);
        roomList.Value = roomListCache.ToList();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.GameIdAlreadyExists && createRetryCount < MAX_CREATE_RETRY)
        {
            createRetryCount++;
            TryCreateRoom();
            return;
        }

        Debug.LogWarning($"[NetworkFlowManager] 방 생성 실패 ({returnCode}): {message}");
        RaiseError("방을 만들지 못했습니다. 다시 시도해주세요.");
    }

    public override void OnJoinedRoom()
    {
        SetState(FlowState.Room);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[NetworkFlowManager] 방 입장 실패 사유 : {message} , (코드){returnCode}");

        string msg = null;

        switch (returnCode)
        {
            case ErrorCode.GameFull:
                msg = "방이 가득 찼습니다.";
                break;

            case ErrorCode.GameClosed:
                msg = "이미 게임이 시작된 방입니다.";
                break;

            case ErrorCode.GameDoesNotExist:
                msg = "방을 찾을 수 없습니다.";
                break;

            default:
                msg = "방에 입장하지 못했습니다.";
                break;
        }

        RaiseError(msg);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.NoRandomMatchFound)
        {
            // TODO : MAX PLAYER CONST 두기
            CreateRoomInternal("", 5, true);
            return;
        }

        Debug.LogWarning($"[NetworkFlowManager] 빠른 입장 실패 ({returnCode}): {message}");
        RaiseError("빠른 입장에 실패했습니다.");
    }

    #endregion

    #region 내부
    // 캐시를 비우고 UI에도 빈 목록을 알림 (로비 입장, 연결 끊김 시 호출)
    private void ClearRoomList()
    {
        roomListCache.Clear();
        roomList.Value = roomListCache.ToList();
    }
    #endregion

    #endregion

    //===============================================================

    #region Room

    #region 요청

    public void RequestLeaveRoom()
    {
        if (CurrentState != FlowState.Room || !PhotonNetwork.InRoom)
            return;

        PhotonNetwork.LeaveRoom();
    }

    #endregion

    #region Photon Callbacks



    #endregion

    #endregion

    //===============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region Debug
    public void Debug_SetState(FlowState newState) => SetState(newState);
    public void Debug_RaiseError(string message) => RaiseError(message);
    #endregion
#endif
}
