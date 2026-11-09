#if UNITY_EDITOR

using UnityEngine;
using Photon.Pun;

public class OfflineModeTest : MonoBehaviour
{
    public bool test = true;

    // 오프라인 테스트용: 서버 없이 마스터 방을 만든다
    private void Awake()
    {
        if (!test) return;
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("OfflineTest");
        }
    }
}
#endif