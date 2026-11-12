using Cysharp.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using R3;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;

public class NetworkFlowManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static NetworkFlowManager Instance { get; private set; }

    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(10);


    private ReactiveProperty<FlowState> state;
    private Subject<string> error;
    private FirebaseAuthService authService = new FirebaseAuthService();
    private RoomMatchService match;         // 방 목록·생성·입장·퀵매치 (Awake에서 생성)
    private NicknameService nicknameService;    // 닉네임 검증·저장·Photon 반영 (Awake에서 생성)
    private RoomSession room;               // 방 안의 플레이어·레디 관리 (Awake에서 생성)
    private RoomChatService chat;           // 방 안 채팅 관리
    private PhotonPlayer pendingNewMaster;  // 방장 변경 안내를 퇴장 안내 뒤에 띄우려고 잠시 보관
    private string pendingReturnError;      // 인게임에서 아웃게임으로 복귀한 뒤 띄울 에러
    private bool isLeavingToLobby;          // 멀티 인게임에서 나가는 중 (방 나가기 → 로비 입장까지)


    // 실제 값은 NicknameService가 가짐. UI는 기존처럼 매니저를 통해 구독
    public ReadOnlyReactiveProperty<string> Nickname => nicknameService.Nickname;
    public bool HasSavedNickname => nicknameService.HasSaved;
    /// <summary> Firebase 익명 로그인 UID. 온라인 접속(로그인) 전·오프라인에서는 null </summary>
    public string UserId => authService.UserId;
    public ReadOnlyReactiveProperty<FlowState> State => state;
    // 실제 값은 RoomMatchService가 가짐. UI는 기존처럼 매니저를 통해 구독
    public ReadOnlyReactiveProperty<IReadOnlyList<RoomInfo>> RoomList => match.RoomList;
    // 실제 값은 RoomSession이 가짐. UI는 기존처럼 매니저를 통해 구독
    public ReadOnlyReactiveProperty<IReadOnlyList<PhotonPlayer>> Players => room.Players;
    public ReadOnlyReactiveProperty<bool> CanStart => room.CanStart;
    public IReadOnlyList<ChatMessage> ChatHistory => chat.History;
    public Observable<ChatMessage> ChatReceived => chat.Received;
    public Observable<string> Error => error;
    public FlowState CurrentState => state.Value;

    //===============================================================

    #region LifeCycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destroy는 프레임 끝에 실행됨 → 그 사이 OnEnable에서 Photon 콜백 대상으로 등록되어
            // 초기화 안 된 중복 매니저가 콜백을 받지 않도록 먼저 비활성화
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Master 씬 로드시 전원이 같은 씬으로 이동
        PhotonNetwork.AutomaticallySyncScene = true;

        // 상태 : 구독하는 순간 현재 값 전달, 같은 값으로 바꾸면 알림X
        state = new ReactiveProperty<FlowState>(FlowState.Title);

        // 에러: 구독 이후에 발생한 메세지만 전달
        error = new Subject<string>();

        // 서비스는 error 생성 이후에 만들어야 함 (생성자에서 RaiseError를 받음)
        match = new RoomMatchService(RaiseError);

        room = new RoomSession();

        // 상태 변경 로그 (구독권을 매니저 수명에 묶음 → 매니저 파괴 시 자동 해제)
        state.Subscribe(s => Debug.Log($"[NetworkFlowManager] 상태 변경: {s}")).AddTo(this);

        // 저장된 닉네임 불러오기 + PhotonNetwork.NickName 반영 (error 생성 이후에 만들어야 함)
        nicknameService = new NicknameService(RaiseError);

        chat = new RoomChatService();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        // 매니저가 직접 만든 소스 정리
        // ?. : 중복 매니저는 Awake에서 return해서 null이므로 건너뜀
        state?.Dispose();
        error?.Dispose();
        match?.Dispose();
        nicknameService?.Dispose();
        room?.Dispose();
        chat?.Dispose();

        SceneManager.sceneLoaded -= OnSceneLoaded;
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

    // ── Request 함수 입구 검사 ──
    // 조건이 맞지 않으면 무시 로그를 남기고 false. caller는 컴파일러가 호출한 함수 이름을 자동으로 넣음

    /// <summary> 현재 상태가 expected일 때만 true </summary>
    private bool Require(FlowState expected, [CallerMemberName] string caller = "")
    {
        if (CurrentState == expected)
            return true;

        Debug.Log($"[NetworkFlowManager] {caller} 무시됨: 현재 상태 = {CurrentState}");
        return false;
    }

    /// <summary> 로비 상태이고 PUN도 로비에 들어가 있을 때만 true (방 생성·입장·퀵매치 요청 중이면 false) </summary>
    private bool RequireLobbyReady([CallerMemberName] string caller = "")
    {
        if (CurrentState == FlowState.Lobby && PhotonNetwork.NetworkClientState == ClientState.JoinedLobby)
            return true;

        Debug.Log($"[NetworkFlowManager] {caller} 무시됨: 상태 = {CurrentState}, PUN = {PhotonNetwork.NetworkClientState}");
        return false;
    }

    /// <summary> 방 상태이고 실제로 방 안에 있을 때만 true (방 나가는 중이면 false) </summary>
    private bool RequireInRoom([CallerMemberName] string caller = "")
    {
        if (CurrentState == FlowState.Room && PhotonNetwork.InRoom)
            return true;

        Debug.Log($"[NetworkFlowManager] {caller} 무시됨: 상태 = {CurrentState}, 방 안 = {PhotonNetwork.InRoom}");
        return false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.IN_GAME)
            SetState(FlowState.InGame);

        else if (scene.name == SceneNames.OUT_GAME && CurrentState == FlowState.LeavingGame)
        {
            // 멀티 나가기: 씬이 먼저 바뀌고 로비 입장은 아직 → 접속 중 화면으로 대기, OnJoinedLobby에서 Lobby
            // (Title로 두면 로비 입장 전까지 타이틀 버튼이 눌려 접속이 꼬일 수 있음)
            // 싱글 나가기·인게임 중 연결 끊김: 타이틀로
            SetState(isLeavingToLobby ? FlowState.Connecting : FlowState.Title);
            RaisePendingReturnErrorAsync().Forget();
        }
    }

    // 아웃게임 복귀 전에 발생한 에러를 복귀 후에 알림
    private async UniTaskVoid RaisePendingReturnErrorAsync()
    {
        string message = pendingReturnError;
        pendingReturnError = null;

        if (string.IsNullOrEmpty(message))
            return;

        // sceneLoaded는 새 씬의 Start보다 먼저 호출됨
        // OutGameUI는 Start에서 Error를 구독하므로 한 프레임 뒤에 알림
        bool canceled = await UniTask.NextFrame(this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();

        if (canceled)
            return;

        RaiseError(message);
    }

    #endregion

    //===============================================================

    #region Connect

    #region 요청

    // 다른 Request 함수와 같은 형태 (호출하는 쪽에서 .Forget() 불필요)
    public void RequestConnectOnline()
    {
        // 첫 await 전에 상태를 Connecting으로 바꾸므로, 여기서 검사해도 중복 접속이 막힘
        if (!Require(FlowState.Title)) return;

        ConnectOnlineAsync().Forget();
    }

    #endregion

    #region 내부

    // 상태 검사는 RequestConnectOnline에서 함
    private async UniTaskVoid ConnectOnlineAsync()
    {
        if (PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = false;
        }

        SetState(FlowState.Connecting);

        // 매니저가 파괴되면 대기 중인 로그인을 취소
        CancellationToken destroyToken = this.GetCancellationTokenOnDestroy();

        try
        {
            await authService.SignInAsync()
                .AttachExternalCancellation(destroyToken)
                .Timeout(LoginTimeout);
        }
        catch (OperationCanceledException)
        {
            // 매니저 파괴로 취소됨 → 아무것도 하지 않음
            return;
        }
        catch (TimeoutException e)
        {
            Debug.LogException(e);
            FailConnecting("Login timed out");
            return;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            FailConnecting("Login failed");
            return;
        }

        // 로그인을 기다리는 동안 상태가 바뀌었으면 접속을 이어가지 않음
        if (CurrentState != FlowState.Connecting)
        {
            Debug.Log($"[NetworkFlowManager] 로그인 완료 후 접속 중단: 현재 상태 = {CurrentState}");
            return;
        }

        Debug.Log($"[NetworkFlowManager] Firebase UID: {authService.UserId}");

        if (!PhotonNetwork.ConnectUsingSettings())   // true = 접속 시도를 시작, false = 시도조차 실패
        {
            FailConnecting("Failed to connect to server");
        }
    }

    // 접속 중일 때만 실패 처리 (그 사이 다른 상태로 바뀌었으면 덮어쓰지 않음)
    private void FailConnecting(string message)
    {
        if (CurrentState != FlowState.Connecting)
            return;

        RaiseError(message);
        SetState(FlowState.Title);
    }

    #endregion

    #region Photon Callbacks

    public override void OnConnectedToMaster()
    {
        // 멀티 나가기 중이면 방을 나와 마스터 서버로 돌아온 것 → 아래에서 로비 입장
        if(CurrentState == FlowState.LeavingGame && !isLeavingToLobby)
        {
            Debug.Log("[NetworkFlowManager] 인게임 나가는 중이라 마스터 접속 처리 무시");
            return;
        }

        if(PhotonNetwork.OfflineMode)
        {
            CreateOfflineRoom();
            return;
        }

        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        // 멀티 나가기 완료 (씬 로드보다 먼저 와도 됨 → OnSceneLoaded는 Lobby 상태라 건드리지 않음)
        isLeavingToLobby = false;

        match.ClearRoomList();
        SetState(FlowState.Lobby);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        // 멀티 나가기 도중 끊기면 로비로 못 감 → 씬 로드 후 타이틀로 가도록 해제
        isLeavingToLobby = false;

        match.ClearRoomList();
        room.Clear();
        room.ResetRoomScopedProps();
        chat.Clear();       // 방 안에서 끊기면 OnLeftRoom이 오지 않을 수 있으므로 여기서도 비움
        pendingNewMaster = null;

        if (CurrentState == FlowState.LeavingGame) return;

        // 직접 연결을 끊은 경우(타이틀로 돌아가기)는 오류로 알리지 않음
        string message = cause != DisconnectCause.DisconnectByClientLogic
            ? $"Disconnected from server. ({cause})"
            : null;

        // 인게임 중 끊김: 상태만 바꾸면 인게임 씬에 남으므로 아웃게임 씬으로 복귀
        // 에러 구독자(OutGameUI)가 인게임 씬에는 없으므로 복귀 후 알림
        if (CurrentState == FlowState.InGame)
        {
            pendingReturnError = message;
            SetState(FlowState.LeavingGame);
            SceneManager.LoadScene(SceneNames.OUT_GAME);
            return;
        }

        SetState(FlowState.Title);

        if (message != null)
            RaiseError(message);
    }

    #endregion

    #endregion

    //===============================================================

    #region Lobby

    #region 요청

    // 로비에서 타이틀로: 서버 연결을 끊으면 OnDisconnected에서 Title 상태로 바뀜
    public void RequestReturnToTitle()
    {
        if (!Require(FlowState.Lobby)) return;

        PhotonNetwork.Disconnect();
    }

    public void RequestCreateRoom(string displayName, int maxPlayers, bool isPublic)
    {
        if (!RequireLobbyReady()) return;

        match.CreateRoom(displayName, maxPlayers, isPublic);
    }

    public void RequestJoinRoom(string roomCode)
    {
        if (!RequireLobbyReady()) return;

        match.JoinRoom(roomCode);
    }

    public void RequestJoinByCode(string roomCode)
    {
        // 상태 검사를 먼저: 로비가 아닐 때 형식 오류 팝업이 뜨지 않게
        if (!RequireLobbyReady()) return;

        // 형식이 틀리면 RoomMatchService가 에러를 알림
        if (!match.TryNormalizeCode(roomCode, out string code))
            return;

        RequestJoinRoom(code);
    }

    public void RequestQuickMatch()
    {
        if (!RequireLobbyReady()) return;

        match.QuickMatch();
    }

    #endregion

    #region Photon Callbacks
    public override void OnRoomListUpdate(List<RoomInfo> changedRoomList)
    {
        match.ApplyRoomListUpdate(changedRoomList);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        match.HandleCreateRoomFailed(returnCode, message);
    }

    public override void OnJoinedRoom()
    {
        // 인게임에서 나갈 때 꺼 둔 씬 동기화를 다시 켬 (방장이 LoadLevel하면 전원 같은 씬으로)
        PhotonNetwork.AutomaticallySyncScene = true;

       if (PhotonNetwork.OfflineMode)
        {
            StartGameInternal();
            return;
        }

        // 순서: 목록·채팅 먼저 → 상태. RoomPanel이 켜질 때 목록·채팅이 이미 채워져 있도록
        room.Refresh();

        // 이전 방 채팅이 남지 않도록 비우고 입장 안내
        chat.Clear();
        chat.AddSystem("방에 입장했습니다");

        SetState(FlowState.Room);
        //PhotonNetwork.LocalPlayer.SetCustomProperties( )
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        match.HandleJoinRoomFailed(returnCode, message);
    }

    // 들어갈 방이 없으면 RoomMatchService가 새 공개 방을 만듦
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        match.HandleJoinRandomFailed(returnCode, message);
    }

    #endregion

    #endregion

    //===============================================================

    #region Room

    #region 요청

    public void RequestLeaveRoom()
    {
        if (!RequireInRoom()) return;

        PhotonNetwork.LeaveRoom();
    }

    public void RequestToggleReady()
    {
        if (!RequireInRoom()) return;

        room.ToggleReady();
    }

    public void RequestStartGame()
    {
        if (!RequireInRoom()) return;

        // 아래는 시작 조건별로 사유가 달라서 개별 검사

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[NetworkFlowManager] 시작 무시됨: 방장 아님");
            return;
        }

        if (!PhotonNetwork.CurrentRoom.IsOpen)
        {
            Debug.Log("[NetworkFlowManager] 시작 무시됨: 이미 시작된 방");
            return;
        }

        if(!room.CheckCanStart())
        {
            Debug.Log("[NetworkFlowManager] 시작 무시됨: 시작 조건 미충족");
            return;
        }

        StartGameInternal();

    }

    public void RequestSendChat(string text)
    {
        if(!RequireInRoom()) return;
        chat.Send(text);
    }

    #endregion

    #region Photon Callbacks

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        room.Refresh();
        chat.AddSystem($"{newPlayer.NickName}님이 입장했습니다");
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        room.Refresh();
        chat.AddSystem($"{otherPlayer.NickName}님이 나갔습니다");

        // 방장이 나간 경우: 미뤄 둔 방장 변경 안내를 퇴장 안내 뒤에 표시
        if (pendingNewMaster != null)
        {
            chat.AddSystem($"{pendingNewMaster.NickName}님이 방장이 되었습니다");
            pendingNewMaster = null;
        }
    }

    // 방장이 나가면 PUN은 OnMasterClientSwitched → OnPlayerLeftRoom 순서로 부름 (LoadBalancingClient 퇴장 이벤트 처리)
    // 그대로 쓰면 "B님이 방장이 되었습니다" → "A님이 나갔습니다"로 뒤바뀌어 보이므로 안내만 퇴장 뒤로 미룸
    // (방장을 직접 넘기는 SetMasterClient는 쓰지 않으므로 방장 변경은 항상 퇴장과 함께 옴)
    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        room.Refresh();
        pendingNewMaster = newMasterClient;
    }

    public override void OnLeftRoom()
    {
        room.Clear();
        room.ResetRoomScopedProps();
        chat.Clear();
        pendingNewMaster = null;
    }

    public override void OnPlayerPropertiesUpdate(PhotonPlayer targetPlayer, Hashtable changedProps)
    {
        room.Refresh();
    }

    #endregion

    #region 내부

    private void StartGameInternal()        // 오프라인 게임도 여기로 시작
    {
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        PhotonNetwork.LoadLevel(SceneNames.IN_GAME);
    }

    #endregion

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.RoomChat) return;
        chat.Receive(photonEvent.Sender, photonEvent.CustomData);
    }

    #endregion

    //===============================================================

    #region Offline

    private const string OFFLINE_ROOM_NAME = "Offline";

    #region 요청

    public void RequestEnterOffline()
    {
        if (!Require(FlowState.Title)) return;

        SetState(FlowState.OfflineSetup);
    }

    public void RequestLeaveOffline()
    {
        if (!Require(FlowState.OfflineSetup)) return;

        SetState(FlowState.Title);
    }

    public void RequestStartOffline()
    {
        if (!Require(FlowState.OfflineSetup)) return;

        if (PhotonNetwork.OfflineMode)
        {
            Debug.Log($"[NetworkFlowManager] 오프라인 시작 무시됨: 이미 오프라인모드");
            return;
        }

        if (PhotonNetwork.IsConnected)
        {
            Debug.Log($"[NetworkFlowManager] 오프라인 시작 무시됨: 서버 접속중");
            return;
        }

        // OnConnectedToMaster 즉시 호출됨
        PhotonNetwork.OfflineMode = true;
    }

    [ContextMenu("인게임 나가기")]
    public void RequestLeaveGame()
    {
        if (!Require(FlowState.InGame)) return;

        SetState(FlowState.LeavingGame);

        if (PhotonNetwork.OfflineMode)
        {
            // 싱글: 오프라인 방을 닫고 타이틀로 (OnSceneLoaded에서 Title)
            PhotonNetwork.OfflineMode = false;
        }
        else
        {
            // 멀티: 나만 방을 나가고 로비로. 남은 사람은 방에 그대로 (방장이면 다른 사람에게 넘어감)
            // 흐름: LeaveRoom → OnLeftRoom → 마스터 서버 재접속(OnConnectedToMaster) → JoinLobby → OnJoinedLobby(Lobby)
            isLeavingToLobby = true;

            // 씬 동기화 끄기: 켜 둔 채 방장이 아웃게임 씬을 불러오면 PUN이 그 씬을 방 속성에 기록하려 함
            //  → 방을 나가는 중이라 "SetProperties not called ... Leaving" 에러, 성공하면 남은 사람까지 아웃게임으로 끌려감
            // 다시 켜는 곳: OnJoinedRoom (다음 방에 들어갈 때)
            PhotonNetwork.AutomaticallySyncScene = false;

            // false = 요청을 보내지 못함(방 안이 아님 등) → 로비로 갈 수 없으니 연결을 끊고 타이틀로
            // (끊김은 LeavingGame 중이라 OnDisconnected에서 에러 없이 넘어가고, 씬 로드 후 Title)
            if (!PhotonNetwork.LeaveRoom())
            {
                Debug.LogWarning("[NetworkFlowManager] 방 나가기 요청 실패, 연결 종료 후 타이틀로");
                isLeavingToLobby = false;
                PhotonNetwork.Disconnect();
            }
        }

        // 혼자 떠나는 이동이라 동기화 없는 LoadScene 사용 (LoadLevel은 방 전원 이동용)
        SceneManager.LoadScene(SceneNames.OUT_GAME);
    }

    #endregion

    #region 내부

    private void CreateOfflineRoom()
    {
        PhotonNetwork.CreateRoom(OFFLINE_ROOM_NAME, new RoomOptions { MaxPlayers = 1 });
    }

    #endregion

    #endregion

    //===============================================================

    #region Nickname

    #region 요청
    public bool RequestChangeNickname(string nick)
    {
        if (!Require(FlowState.Lobby)) return false;

        // 검증 실패 시 NicknameService가 에러를 알리고 false 반환
        return nicknameService.TryChange(nick);
    }

    #endregion

    #endregion
}
