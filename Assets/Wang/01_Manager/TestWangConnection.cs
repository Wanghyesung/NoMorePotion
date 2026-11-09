using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// TestWang 씬 전용 접속 및 로컬 플레이어 생성.
[DisallowMultipleComponent]
public sealed class TestWangConnection : MonoBehaviourPunCallbacks
{
    [SerializeField] private string roomName = "TestWang";
    [SerializeField, Range(1, 20)] private int maxPlayers = 4;
    private bool joining;
    [SerializeField] private Player playerPrefab;
    [SerializeField] private PlayerUIBinding playerBinder;
    [SerializeField] private Transform spawnPoint;
    private GameObject spawnedPlayer;

    private void Awake()
    {
        // Resources 밖의 직렬화 프리팹을 모든 클라이언트에서 입장 전에 등록한다.
        DefaultPool pool = PhotonNetwork.PrefabPool as DefaultPool;
        if (playerPrefab == null || playerBinder == null || pool == null)
        {
            Debug.LogError("[TestWang] Player Prefab, Player Binder와 Photon DefaultPool을 확인해주세요.", this);
            enabled = false;
            return;
        }
        pool.ResourceCache[playerPrefab.name] = playerPrefab.gameObject;
    }

    private void Start()
    {
        if (PhotonNetwork.OfflineMode == true)
        {
            Debug.LogError("[TestWang] OfflineMode를 끄고 테스트 씬을 실행해주세요.", this);
            return;
        }

        if (PhotonNetwork.InRoom == true)
        {
            Debug.Log($"[TestWang] 이미 입장한 방: {PhotonNetwork.CurrentRoom.Name}", this);
            SpawnPlayer();
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady == true && PhotonNetwork.Server == ServerConnection.MasterServer)
        {
            JoinTestRoom();
            return;
        }

        if (PhotonNetwork.IsConnected == false && PhotonNetwork.ConnectUsingSettings() == false)
            Debug.LogError("[TestWang] 접속을 시작하지 못했습니다. PhotonServerSettings를 확인해주세요.", this);
    }

    public override void OnConnectedToMaster() => JoinTestRoom();

    private void JoinTestRoom()
    {
        if (joining == true || PhotonNetwork.InRoom == true || PhotonNetwork.OfflineMode == true)
            return;
        if (string.IsNullOrWhiteSpace(roomName) == true)
        {
            Debug.LogError("[TestWang] 방 이름을 입력해주세요.", this);
            return;
        }

        // 같은 리전과 버전으로 실행한 클라이언트끼리 이 방에 모인다.
        joining = PhotonNetwork.JoinOrCreateRoom(roomName,
            new RoomOptions { MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 1, 20), IsVisible = false }, TypedLobby.Default);
        if (joining == false)
            Debug.LogError("[TestWang] 방 입장 요청을 보내지 못했습니다.", this);
    }

    public override void OnJoinedRoom()
    {
        joining = false;
        SpawnPlayer();
        Debug.Log($"[TestWang] 입장 완료: {PhotonNetwork.CurrentRoom.Name}, 내 번호: {PhotonNetwork.LocalPlayer.ActorNumber}, 인원: {PhotonNetwork.CurrentRoom.PlayerCount}", this);
    }

    private void SpawnPlayer()
    {
        if (spawnedPlayer != null)
            return;

        Transform point = spawnPoint != null ? spawnPoint : transform;
        spawnedPlayer = PhotonNetwork.Instantiate(playerPrefab.name, point.position, point.rotation);
        playerBinder.SetPlayer(spawnedPlayer.GetComponent<Player>());
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        joining = false;
        Debug.LogError($"[TestWang] 입장 실패 ({returnCode}): {message}", this);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        joining = false;
        Debug.LogError($"[TestWang] 방 생성 실패 ({returnCode}): {message}", this);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        joining = false;
        Debug.LogWarning($"[TestWang] 연결 종료: {cause}", this);
    }
}
