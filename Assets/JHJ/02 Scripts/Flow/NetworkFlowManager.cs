using Cysharp.Threading.Tasks;
using Photon.Pun;
using Photon.Realtime;
using R3;
using System;
using System.Collections.Generic;
using UnityEngine;

public class NetworkFlowManager : MonoBehaviourPunCallbacks
{
    public static NetworkFlowManager Instance { get; private set; }


    private const string NicknameKey = "Nickname";
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromSeconds(10);


    private ReactiveProperty<FlowState> state;
    private ReactiveProperty<List<RoomInfo>> roomList;
    private Subject<string> error;
    private FirebaseAuthService authService = new FirebaseAuthService();
    private RoomListCache roomListCache = new RoomListCache();

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

    #region Photon Callbacks
    public override void OnRoomListUpdate(List<RoomInfo> changedRoomList)
    {
        roomListCache.Apply(changedRoomList);
        roomList.Value = roomListCache.ToList();
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



    //===============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region Debug
    public void Debug_SetState(FlowState newState) => SetState(newState);
    public void Debug_RaiseError(string message) => RaiseError(message);
    #endregion
#endif
}
