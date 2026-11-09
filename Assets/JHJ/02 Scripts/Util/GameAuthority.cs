using Photon.Pun;

/// <summary>
/// 게임 진행·판정 권한 확인. 시계 진행, 승패 판정처럼 한 곳에서만 실행해야 하는 로직에 사용.
/// </summary>
/// <remarks>
/// - 멀티 : 방장만 권한
/// - 오프라인 싱글 : OfflineMode에서는 IsConnected·IsMasterClient 모두 true → 권한
/// - 미접속 (에디터에서 인게임 씬 바로 실행) : 권한. IsMasterClient만 보면 false라 진행이 막힘
/// </remarks>
public static class GameAuthority
{
    public static bool HasAuthority => !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
}
