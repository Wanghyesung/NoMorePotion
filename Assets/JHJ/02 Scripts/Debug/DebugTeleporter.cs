using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [개발용] 키 하나로 방 안의 모든 플레이어를 지정한 위치로 텔레포트합니다.
/// 씬의 아무 오브젝트에 붙이고 목표 위치(Transform)를 연결해서 씁니다.
/// 누가 눌러도 모두에게 이벤트가 가고, 각자 자기 플레이어만 옮깁니다. (플레이어 위치는 소유자가 동기화하므로)
/// 여러 명이 한 점에 겹치지 않도록 ActorNumber 순서대로 원형으로 벌려 세웁니다.
/// </summary>
public class DebugTeleporter : MonoBehaviour, IOnEventCallback
{
    [SerializeField] private Key teleportKey = Key.F8;
    [SerializeField] private Transform[] targets;           // 여러 개면 누를 때마다 다음 위치로 순환
    [SerializeField] private float spreadRadius = 2f;       // 플레이어끼리 벌려 세울 반지름

    private int nextIndex;

    private void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    private void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current[teleportKey].wasPressedThisFrame)
            return;

        if (targets == null || targets.Length == 0)
        {
            Debug.LogWarning("[DebugTeleporter] 목표 위치가 없습니다.");
            return;
        }

        int index = nextIndex;
        nextIndex = (nextIndex + 1) % targets.Length;

        // 나는 바로 이동, 다른 사람에게는 이벤트로 알림 (GameStarting 과 같은 방식)
        TeleportLocal(index);

        if (PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode)
            PhotonNetwork.RaiseEvent(
                NetEventCodes.DebugTeleport,
                index,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != NetEventCodes.DebugTeleport)
            return;

        int index = (int)photonEvent.CustomData;
        nextIndex = (index + 1) % Mathf.Max(1, targets.Length);    // 다음에 누가 눌러도 같은 순서를 이어감
        TeleportLocal(index);
    }

    private void TeleportLocal(int index)
    {
        if (targets == null || index < 0 || index >= targets.Length || targets[index] == null)
        {
            Debug.LogWarning($"[DebugTeleporter] {index}번 목표 위치가 없습니다.");
            return;
        }

        global::Player local = FindLocalPlayer();
        if (local == null)
        {
            Debug.LogWarning("[DebugTeleporter] 내 플레이어를 찾지 못했습니다.");
            return;
        }

        Transform target = targets[index];
        Vector3 position = target.position + GetSpreadOffset();

        local.transform.SetPositionAndRotation(position, target.rotation);

        Rigidbody body = local.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.rotation = target.rotation;
            body.linearVelocity = Vector3.zero;
        }

        Debug.Log($"[DebugTeleporter] {target.name} 으로 이동");
    }

    // 방 인원 중 내 순서에 맞춰 원 위의 자리를 고름 (모두 같은 PlayerList 순서를 보므로 겹치지 않음)
    private Vector3 GetSpreadOffset()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList.Length <= 1)
            return Vector3.zero;

        Photon.Realtime.Player[] players = PhotonNetwork.PlayerList;    // ActorNumber 순 정렬
        int myOrder = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);
        float angle = myOrder * Mathf.PI * 2f / players.Length;

        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spreadRadius;
    }

    private static global::Player FindLocalPlayer()
    {
        foreach (global::Player player in FindObjectsByType<global::Player>(FindObjectsSortMode.None))
        {
            if (player.IsLocalPlayer)
                return player;
        }
        return null;
    }
}
