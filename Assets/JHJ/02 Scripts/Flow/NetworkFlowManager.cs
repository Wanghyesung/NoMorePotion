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
    public static NetworkFlowManager Instance { get; private set; }

    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(10);


    private ReactiveProperty<FlowState> state;
    private Subject<string> error;
    private FirebaseAuthService authService = new FirebaseAuthService();
    private RoomMatchService match;         // 방 목록·생성·입장·퀵매치 (Awake에서 생성)
    private NicknameService nicknameService;    // 닉네임 검증·저장·Photon 반영 (Awake에서 생성)
    private RoomSession room;               // 방 안의 플레이어·레디 관리 (Awake에서 생성)
    private string pendingReturnError;      // 인게임에서 아웃게임으로 복귀한 뒤 띄울 에러


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

    // 다른 Request 함수와 같은 형태 (호출하는 쪽에서 .Forget() 불필요)
    public void RequestConnectOnline()
    {
        ConnectOnlineAsync().Forget();
    }

    #endregion

    #region 내부

    private async UniTaskVoid ConnectOnlineAsync()
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
        match.ClearRoomList();
        SetState(FlowState.Lobby);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        match.ClearRoomList();
        room.Clear();
        room.ResetReady();

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

        match.CreateRoom(displayName, maxPlayers, isPublic);
    }

    public void RequestJoinRoom(string roomCode)
    {
        if (CurrentState != FlowState.Lobby || PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
            return;

        match.JoinRoom(roomCode);
    }

    public void RequestJoinByCode(string roomCode)
    {
        // 형식이 틀리면 RoomMatchService가 에러를 알림
        if (!match.TryNormalizeCode(roomCode, out string code))
            return;

        RequestJoinRoom(code);
    }

    public void RequestQuickMatch()
    {
        if (CurrentState != FlowState.Lobby || PhotonNetwork.NetworkClientState != ClientState.JoinedLobby)
        {
            Debug.Log($"[NetworkFlowManager] 퀵매치 무시됨 - 상태: {CurrentState}, PUN: {PhotonNetwork.NetworkClientState}");
            return;
        }

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
       if (PhotonNetwork.OfflineMode)
        {
            StartGameInternal();
            return;
        }

        // 순서: 목록 먼저 → 상태. RoomPanel이 켜질 때 목록이 이미 채워져 있도록
        room.Refresh();
        SetState(FlowState.Room);
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

        room.ToggleReady();
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

        if(!room.CheckCanStart())
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
        room.Refresh();
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        room.Refresh();
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        room.Refresh();
    }

    public override void OnLeftRoom()
    {
        room.Clear();
        room.ResetReady();
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

        // 검증 실패 시 NicknameService가 에러를 알리고 false 반환
        return nicknameService.TryChange(nick);
    }

    #endregion

    #endregion
}
