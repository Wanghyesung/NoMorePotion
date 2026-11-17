#if UNITY_EDITOR

using Photon.Pun;
using Photon.Realtime;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 에디터 테스트용입니다. 타이틀과 로비를 거치지 않고 이 씬을 바로 실행해도 방에 들어간 상태가 되게 합니다.
/// - 오프라인 : 서버 없이 나 혼자 마스터인 방을 만듭니다. (기존 동작)
/// - 온라인 : 포톤 서버에 접속해서 같은 이름의 방이 없으면 만들고, 있으면 들어갑니다. 먼저 들어간 쪽이 방장입니다.
///   방에 들어가기 전에 GameSessionManager 가 시작하면 플레이어를 만들지 못하고 멈추므로, 방에 들어갈 때까지 그 시작을 미룹니다.
///
/// 주의 : 테스트 전용이라 실제 규칙(늦은 입장 없음)과 달리 나중에 들어가도 됩니다. 에디터에서만 존재하므로 빌드에는 들어가지 않습니다.
/// </summary>
public class OfflineModeTest : MonoBehaviourPunCallbacks
{
    #region Settings
    public enum TestMode
    {
        Offline,
        Online,
    }

    public bool test = true;

    [SerializeField, LabelText("테스트 모드")]
    private TestMode _mode = TestMode.Offline;

    // 같은 포톤 앱을 다른 사람도 쓰므로, 겹치지 않는 이름으로 바꿔 쓰세요. 같은 이름이면 같은 방으로 들어갑니다.
    [SerializeField, LabelText("온라인 방 이름"), ShowIf("_mode", TestMode.Online)]
    private string _onlineRoomName = "KHJ_OnlineTest";

    [SerializeField, LabelText("온라인 최대 인원"), Range(1, 5), ShowIf("_mode", TestMode.Online)]
    private int _onlineMaxPlayers = 5;

    // 방에 들어갈 때까지 시작을 미뤄 둔 세션입니다.
    private GameSessionManager _gatedSession;
    private bool _waitingForRoom;
    #endregion

    #region Start
    // 오프라인 : 서버 없이 마스터 방을 만든다 / 온라인 : 서버에 접속해서 방에 들어간다
    private void Awake()
    {
        if (!test) return;

        if (_mode == TestMode.Online)
        {
            StartOnline();
            return;
        }

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("OfflineTest");
        }
    }

    private void StartOnline()
    {
        // 로비 흐름으로 이미 방에 들어와 있으면 그 방을 그대로 씁니다.
        if (PhotonNetwork.InRoom) return;

        // 창이 포커스를 잃어도 멈추지 않게 합니다. (실행 중에만 바뀌는 값이라 프로젝트 설정은 건드리지 않음)
        Application.runInBackground = true;

        // 방에 들어가기 전에 세션이 시작되면 플레이어를 만들지 못하고 멈춥니다. 모든 Awake 가 끝난 뒤에 Start 가 불리므로 지금 꺼 두면 됩니다.
        _gatedSession = FindFirstObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        if (_gatedSession != null)
        {
            _gatedSession.enabled = false;
        }

        _waitingForRoom = true;
        PhotonNetwork.OfflineMode = false;
        PhotonNetwork.NickName = $"Test{Random.Range(1000, 9999)}";

        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinTestRoom();
        }
        else if (!PhotonNetwork.ConnectUsingSettings())
        {
            Debug.LogWarning("[OfflineModeTest] : 포톤 서버에 접속을 시작하지 못했습니다. 세션 시작을 미뤄 둔 상태로 둡니다.");
        }
    }

    private void JoinTestRoom()
    {
        // 로비 목록에 보이지 않는 방으로 만듭니다. (이름을 아는 사람만 들어옴)
        var options = new RoomOptions { MaxPlayers = _onlineMaxPlayers, IsVisible = false };

        if (!PhotonNetwork.JoinOrCreateRoom(_onlineRoomName, options, TypedLobby.Default))
        {
            Debug.LogWarning($"[OfflineModeTest] : 방 입장 요청을 보내지 못했습니다. ({_onlineRoomName})");
        }
    }
    #endregion

    #region Callbacks
    public override void OnConnectedToMaster()
    {
        if (!_waitingForRoom || PhotonNetwork.InRoom) return;

        JoinTestRoom();
    }

    public override void OnJoinedRoom()
    {
        if (!_waitingForRoom) return;

        _waitingForRoom = false;
        Debug.Log($"[OfflineModeTest] : 방 '{_onlineRoomName}' 에 들어왔습니다. 방장 : {PhotonNetwork.IsMasterClient} / 인원 : {PhotonNetwork.CurrentRoom.PlayerCount}");

        // 이제 세션이 시작하면 플레이어를 만들 수 있습니다.
        if (_gatedSession != null)
        {
            _gatedSession.enabled = true;
            _gatedSession = null;
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (!_waitingForRoom) return;

        // 세션은 계속 미뤄 둡니다. (그대로 시작하면 방 입장 오류만 나고 멈춤)
        Debug.LogWarning($"[OfflineModeTest] : 방 입장 실패 ({returnCode}) {message}. 방이 가득 찼거나 같은 이름의 다른 방과 충돌했을 수 있습니다.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!_waitingForRoom) return;

        Debug.LogWarning($"[OfflineModeTest] : 포톤 접속이 끊겼습니다. ({cause})");
    }
    #endregion
}
#endif
