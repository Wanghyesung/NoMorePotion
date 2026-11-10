using Photon.Pun;

/// <summary>
/// AI 판단 권한 체크
/// 오프라인(포톤 미연결)이면 항상 true, 온라인이면 마스터만 true
/// </summary>
public static class NetAuthority
{
    public static bool IsAuthority => !PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient;
}
