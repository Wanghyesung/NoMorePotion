using Cysharp.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using R3;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;
using UnityEngine.SceneManagement;

public class NetworkFlowManager : MonoBehaviourPunCallbacks
{
    private const int MAX_CREATE_RETRY = 5;

    public static NetworkFlowManager Instance { get; private set; }

    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(10);


    private ReactiveProperty<FlowState> state;
    private ReactiveProperty<List<RoomInfo>> roomList;
    private Subject<string> error;
    private FirebaseAuthService authService = new FirebaseAuthService();
    private RoomListCache roomListCache = new RoomListCache();
    private int createRetryCount;
    private RoomOptions pendingRoomOptions;
    private NicknameStore nicknameStore = new NicknameStore();
    private ReactiveProperty<string> nickname;
    private ReactiveProperty<List<PhotonPlayer>> players;
    private ReactiveProperty<bool> canStart;
    private string pendingReturnError;      // 인게임에서 아웃게임으로 복귀한 뒤 띄울 에러


    public ReadOnlyReactiveProperty<string> Nickname => nickname;
    public ReadOnlyReactiveProperty<FlowState> State => state;
    public ReadOnlyReactiveProperty<List<RoomInfo>> RoomList => roomList;
    public ReadOnlyReactiveProperty<List<PhotonPlayer>> Players => players;
    public ReadOnlyReactiveProperty<bool> CanStart => canStart;
    public Observable<string> Error => error;
    public FlowState CurrentState => state.Value;
    public bool HasSavedNickname { get; private set; }

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

        roomList = new ReactiveProperty<List<RoomInfo>>(new List<RoomInfo>());

        players = new ReactiveProperty<List<PhotonPlayer>>(new List<PhotonPlayer>());

        canStart = new ReactiveProperty<bool>(false);

        // 상태 변경 로그 (구독권을 매니저 수명에 묶음 → 매니저 파괴 시 자동 해제)
        state.Subscribe(s => Debug.Log($"[NetworkFlowManager] 상태 변경: {s}")).AddTo(this);

        string savedNickname = nicknameStore.Load();
        HasSavedNickname = !string.IsNullOrEmpty(savedNickname);

        nickname = new ReactiveProperty<string>(HasSavedNickname ? savedNickname : nicknameStore.CreateDefault() );
        PhotonNetwork.NickName = nickname.Value;

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
        nickname?.Dispose();
        players?.Dispose();
        canStart?.Dispose();

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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.IN_GAME)
            SetState(FlowState.InGame);

        else if (scene.name == SceneNames.OUT_GAME && CurrentState == FlowState.LeavingGame)
        {
            SetState(FlowState.Title);
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

    public async UniTaskVoid RequestConnectOnline()
    {
        if (CurrentState != FlowState.Title) return;

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

    #endregion

    #region 내부

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

        if(CurrentState == FlowState.LeavingGame)
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
        ClearRoomList();
        SetState(FlowState.Lobby);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        ClearRoomList();
        ClearPlayers();
        ResetReady();

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
        if (CurrentState != FlowState.Lobby)
        {
            Debug.Log($"[NetworkFlowManager] 타이틀로 돌아가기 무시됨: 현재 상태 = {CurrentState}");
            return;
        }

        PhotonNetwork.Disconnect();
    }

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
            { RoomProps.DISPLAY_NAME, displayName }
        },

            // 로비목록에서도 보이게 할 키 목록
            CustomRoomPropertiesForLobby = new[] { RoomProps.DISPLAY_NAME }
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
            RejoinLobby();
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

        if (code.Length != RoomCodeGenerator.CODE_LENGTH)
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
            RejoinLobby();
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
        RejoinLobby();
    }

    public override void OnJoinedRoom()
    {
       if (PhotonNetwork.OfflineMode)
        {
            StartGameInternal();
            return;
        }

        RefreshPlayers();
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
        RejoinLobby();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (returnCode == ErrorCode.NoRandomMatchFound)
        {
            CreateRoomInternal("", RoomRules.MAX_PLAYERS, true);
            return;
        }

        Debug.LogWarning($"[NetworkFlowManager] 빠른 입장 실패 ({returnCode}): {message}");
        RaiseError("빠른 입장에 실패했습니다.");
        RejoinLobby();
    }

    #endregion

    #region 내부
    // 캐시를 비우고 UI에도 빈 목록을 알림 (로비 입장, 연결 끊김 시 호출)
    private void ClearRoomList()
    {
        roomListCache.Clear();
        roomList.Value = roomListCache.ToList();
    }

    // 방 입장, 생성 요청 시 PUN이 로비에서 빠지고 실패하면 마스터 서버에만 남음 → 로비 재입장 필요
    private void RejoinLobby()
    {
        if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer)
            PhotonNetwork.JoinLobby();
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

    public void RequestToggleReady()
    {
        if (CurrentState != FlowState.Room || !PhotonNetwork.InRoom)
        {
            Debug.Log($"[NetworkFlowManager] 준비 무시됨: 현재 위치 = {CurrentState}");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[NetworkFlowManager] 준비 무시됨: 현재 방장");
            return;
        }

        bool isReady = PlayerProps.IsReady(PhotonNetwork.LocalPlayer);
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PlayerProps.READY, !isReady } });
    }

    public void RequestStartGame()
    {
        if (CurrentState != FlowState.Room || !PhotonNetwork.InRoom)
        {
            Debug.Log($"[NetworkFlowManager] 시작 무시됨: 현재 위치 = {CurrentState}");
            return;
        }

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

        if(!CheckCanStart())
        {
            Debug.Log("[NetworkFlowManager] 시작 무시됨: 시작할 수 있지않음");
            return;
        }

        StartGameInternal();

    }

    #endregion

    #region Photon Callbacks

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        RefreshPlayers();
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        RefreshPlayers();
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        RefreshPlayers();
    }

    public override void OnLeftRoom()
    {
        ClearPlayers();
        ResetReady();
    }

    public override void OnPlayerPropertiesUpdate(PhotonPlayer targetPlayer, Hashtable changedProps)
    {
        RefreshPlayers();
    }

    #endregion

    #region 내부

    private void RefreshPlayers()
    {
        players.Value = new List<PhotonPlayer>(PhotonNetwork.PlayerList);
        canStart.Value = CheckCanStart();
    }

    private void ClearPlayers()
    {
        players.Value = new List<PhotonPlayer>();
    }

    private bool CheckCanStart()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        foreach(PhotonPlayer player in PhotonNetwork.PlayerList)
        {
            if (player.IsMasterClient)
                continue;

            if (!PlayerProps.IsReady(player))
                return false;
        }

        return true;
    }

    private void ResetReady()
    {
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PlayerProps.READY, false } });
    }

    private void StartGameInternal()        // 오프라인 게임도 여기로 시작
    {
        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        PhotonNetwork.LoadLevel(SceneNames.IN_GAME);
    }

    #endregion

    #endregion

    //===============================================================

    #region Offline

    private const string OFFLINE_ROOM_NAME = "Offline";

    #region 요청

    public void RequestEnterOffline()
    {
        if (CurrentState != FlowState.Title)
        {
            Debug.Log($"[NetworkFlowManager] 싱글 진입 무시됨: 현재 상태 = {CurrentState}");
            return;
        }

        SetState(FlowState.OfflineSetup);
    }

    public void RequestLeaveOffline()
    {
        if (CurrentState != FlowState.OfflineSetup)
        {
            Debug.Log($"[NetworkFlowManager] 오프라인 나가기 무시됨: 현재 상태 = {CurrentState}");
            return;
        }

        SetState(FlowState.Title);
    }

    public void RequestStartOffline()
    {
        if (CurrentState != FlowState.OfflineSetup)
        {
            Debug.Log($"[NetworkFlowManager] 오프라인 시작 무시됨: 현재 상태 = {CurrentState}");
            return;
        }

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
        if(CurrentState != FlowState.InGame)
        {
            Debug.Log($"[NetworkFlowManager] 인게임 나가기 무시됨: 현재 상태 = {CurrentState}");
            return;
        }

        // TODO : 멀티 나가기 구현 추후
        if (!PhotonNetwork.OfflineMode)
        {
            Debug.Log($"[NetworkFlowManager] 인게임 나가기 무시됨: 멀티 나가기 미구현");
            return;
        }

        SetState(FlowState.LeavingGame);
        PhotonNetwork.OfflineMode = false;

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
        if (CurrentState != FlowState.Lobby)
        {
            Debug.Log($"[NetworkFlowManager] 닉네임 수정 무시됨: 현재상태 = {CurrentState}");
            return false;
        }

        nick = (nick ?? "").Trim();

        if (string.IsNullOrEmpty(nick))
        {
            RaiseError("닉네임을 입력하세요.");
            return false;
        }

        // [임시] 닉네임 길이 규칙 확인
        if (nick.Length > NicknameStore.MAX_LENGTH)
        {
            RaiseError($"닉네임 글자 수를 {NicknameStore.MAX_LENGTH}자 이내로 입력하세요.");
            return false;
        }

        nicknameStore.Save(nick);
        HasSavedNickname = true;
        nickname.Value = nick;
        PhotonNetwork.NickName = nick;
        return true;
    }

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
