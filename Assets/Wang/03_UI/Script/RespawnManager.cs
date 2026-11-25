using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using R3;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

[RequireComponent(typeof(GameSessionManager), typeof(PhotonView))]
public sealed class RespawnManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private RespawnCountdownUI popup;

    private GameSessionManager session;
    private Player player;
    private Transform spawnPoint;
    private int? localDeathAt;
    private bool ready;
    private Dictionary<int, int> pending;

    // 부활 요청을 처리할 때 확인할 게임 세션을 가져온다.
    private void Awake() => session = GetComponent<GameSessionManager>();

    // 게임이 끝나면 대기 중인 부활 팝업도 닫는다.
    private void Start()
    {
        session.Phase.Where(phase => phase == SessionPhase.Ended)
            .Subscribe(_ => Close()).AddTo(this);
    }

    // 내 플레이어의 사망 이벤트를 구독하고 부활할 마을 위치를 저장한다.
    public void Bind(Player localPlayer, Transform village)
    {
        player = localPlayer;
        spawnPoint = village;
        player.OnDead += OnLocalDeath;
    }

    // 매니저가 사라지면 플레이어의 사망 이벤트 구독을 해제한다.
    private void OnDestroy()
    {
        if (player != null)
            player.OnDead -= OnLocalDeath;
    }

    // 사망 시각을 한 번 기록하고 같은 시각으로 내 UI와 방장의 타이머를 시작한다.
    private void OnLocalDeath(Vector3 position)
    {
        if (localDeathAt.HasValue || session.IsEnded)
            return;

        localDeathAt = PhotonNetwork.ServerTimestamp;
        ready = false;
        popup.Show(localDeathAt.Value);
        ReportDeath();
    }

    // 기록해 둔 사망 시각을 현재 방장에게 보낸다.
    private void ReportDeath() =>
        photonView.RPC(nameof(RpcReportDeath), RpcTarget.MasterClient, localDeathAt.Value);

    // 부활 버튼을 누르기 전에 방장이 바뀌면 최초 사망 시각을 새 방장에게 다시 보낸다.
    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient)
            pending = null;

        if (localDeathAt.HasValue && !session.IsEnded)
            ReportDeath();
    }

    // 방을 나간 플레이어를 부활 대기 목록에서 제거한다.
    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer) =>
        pending?.Remove(otherPlayer.ActorNumber);

    // 방장이 요청자의 사망 시각을 등록하고 남은 시간만큼 대기를 시작한다.
    [PunRPC]
    private void RpcReportDeath(int deathAt, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || info.Sender == null || session.IsEnded)
            return;

        pending ??= new Dictionary<int, int>();
        int actor = info.Sender.ActorNumber;
        if (pending.TryGetValue(actor, out int previous) && deathAt - previous <= 0)
            return;

        pending[actor] = deathAt;
        NotifyWhenReady(info.Sender, deathAt, pending).Forget();
    }

    // 대기 시간이 지나면 방장이 해당 플레이어에게만 부활 승인을 보낸다.
    private async UniTask NotifyWhenReady(PhotonPlayer target, int deathAt, Dictionary<int, int> waits)
    {
        //남은 시간까지 마클에서 대기
        int remaining = popup.RemainingMilliseconds(deathAt, PhotonNetwork.ServerTimestamp);
        while (remaining > 0)
        {
            await UniTask.Delay(remaining, DelayType.Realtime, cancellationToken: destroyCancellationToken);
            remaining = popup.RemainingMilliseconds(deathAt, PhotonNetwork.ServerTimestamp);
        }

        //완료되면 해당 Actor에게 알려주기
        int actor = target.ActorNumber;
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || session.IsEnded ||
            pending != waits || !waits.TryGetValue(actor, out int current) || current != deathAt)
            return;

        waits.Remove(actor);
        photonView.RPC(nameof(RpcRespawnReady), target, deathAt);
    }

    // 현재 방장이 이번 사망을 승인했는지 확인하고 내 부활 버튼을 연다.
    [PunRPC]
    private void RpcRespawnReady(int deathAt, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient || localDeathAt != deathAt || session.IsEnded)
            return;

        ready = true;
        popup.SetReady();
    }

    // MainScene의 부활 버튼을 누르면 승인 상태를 확인한 뒤 마을에서 부활시킨다.
    public void Respawn()
    {
        if (!ready || !localDeathAt.HasValue || session.IsEnded || player.Character.HP > 0)
            return;

        Close();
        player.RespawnAt(spawnPoint);
    }

    // 이번 사망 시각과 승인 상태를 초기화하고 팝업을 닫는다.
    private void Close()
    {
        ready = false;
        localDeathAt = null;
        popup.gameObject.SetActive(false);
    }
}
