using UnityEngine;
using Photon.Pun;
using SF = UnityEngine.SerializeField;
using System.Collections.Generic;
using R3;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using PhotonPlayer = Photon.Realtime.Player;

public class CastleTeleportManager : MonoBehaviourPunCallbacks
{
    public static CastleTeleportManager Instance { get; private set; }

    [SF] private float voteSec = 15f;       // 투표시간
    [SF] private float retryCooldownSec = 3f;   // 재투표 대기시간


    private HashSet<int> agreed = new HashSet<int>(); // 동의한 플레이어 ActorNum
    private int voteDeadline;
    private float cooldownUntil;
    private bool isVoteOpen;
    private bool hasAnswered;
    private int deadline;
    private bool masterVoting;   //방장 전용
    private Player cachedPlayer;


    private Subject<(string requester, int deadline)> voteOpened = new Subject<(string requester, int deadline)>();
    private Subject<(int agreed, int total)> voteProgress = new Subject<(int agreed, int total)>();
    private Subject<(VoteCloseReason reason, string name)> voteClosed = new Subject<(VoteCloseReason reason, string name)>();

    public Observable<(string requester, int deadline)> VoteOpened => voteOpened;
    public Observable<(int agreed, int total)> VoteProgress => voteProgress;
    public Observable<(VoteCloseReason reason, string name)> VoteClosed => voteClosed;
    public CastleStage Stage => RoomProps.GetCastleStage(PhotonNetwork.CurrentRoom);
    public bool HasAnswered => hasAnswered;

    #region 생명주기

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Debug.LogWarning("[CastleTeleportManager] Instance 이미 있음");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        voteOpened?.Dispose();
        voteClosed?.Dispose();
        voteProgress?.Dispose();
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || !masterVoting)
            return;

        if (PhotonNetwork.ServerTimestamp - voteDeadline >= 0)
            CloseVote(VoteCloseReason.TimeOut);
    }
    #endregion


    #region 로컬 요청·응답
    public bool TryRequestVote(out string failReason)
    {
        failReason = "";

        if (!AreaBossProgress.IsAllCleared())
        {
            failReason = "아직 남은 지역보스가 있어요";
            return false;
        }

        if (Stage != CastleStage.Ready)
        {
            failReason = "이미 진행중이에요";
            return false;
        }

        if (!IsLocalActive())
        {
            failReason = "부활 후 요청가능해요";
            return false;
        }

        photonView.RPC(nameof(RpcRequestVote), RpcTarget.MasterClient);
        return true;
    }

    public void Answer(bool yes)
    {
        if (!isVoteOpen || hasAnswered)
            return;

        if (!IsLocalActive())
            return;

        hasAnswered = true;
        photonView.RPC(nameof(RpcAnswer), RpcTarget.MasterClient, yes);
    }
    #endregion

    #region Master판정
    private void SetStage(CastleStage stage)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { RoomProps.CASTLE_STAGE, (int)stage } });
    }

    private void BroadcastProgress()
    {
        photonView.RPC(nameof(RpcVoteProgress), RpcTarget.All, agreed.Count, PhotonNetwork.CurrentRoom.PlayerCount);
    }

    private void CloseVote(VoteCloseReason reason, string name = "")
    {
        masterVoting = false;
        SetStage(CastleStage.Ready);
        cooldownUntil = Time.realtimeSinceStartup + retryCooldownSec;
        agreed.Clear();
        photonView.RPC(nameof(RpcCloseVote), RpcTarget.All, (int)reason, name);
    }

    // 모두 동의
    private void Pass()
    {
        masterVoting = false;
        SetStage(CastleStage.InCastle);
        agreed.Clear();
        photonView.RPC(nameof(RpcCloseVote), RpcTarget.All, (int)VoteCloseReason.Passed, "");
        //TODO: 텔레포트 추가
    }

    private void CheckPassed()
    {
        if (agreed.Count >= PhotonNetwork.CurrentRoom.PlayerCount)
            Pass();
    }

    [PunRPC]
    private void RpcRequestVote(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!AreaBossProgress.IsAllCleared() || masterVoting || Stage != CastleStage.Ready)
            return;

        if (Time.realtimeSinceStartup < cooldownUntil)
        {
            photonView.RPC(nameof(RpcNotify), info.Sender, "잠시 후 다시 시도해주세요.");
            return;
        }

        agreed.Clear();

        if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
            agreed.Add(info.Sender.ActorNumber);

        voteDeadline = PhotonNetwork.ServerTimestamp + (int)(voteSec*1000);     //ServerTimeStamp = 밀리초
        masterVoting = true;
        SetStage(CastleStage.Voting);
        photonView.RPC(nameof(RpcOpenVote), RpcTarget.All, info.Sender.ActorNumber, voteDeadline);
        BroadcastProgress();
    }

    [PunRPC]
    private void RpcAnswer(bool yes, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!masterVoting)
            return;

        if(!yes)
        {
            CloseVote(VoteCloseReason.Rejected, info.Sender.NickName);
            return;
        }

        agreed.Add(info.Sender.ActorNumber);
        BroadcastProgress();
        CheckPassed();
    }
    #endregion

    #region 각자 수신
    [PunRPC]
    private void RpcNotify(string msg, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient)
            return;

        NetworkFlowManager.Instance?.AddSystemChat(msg);
    }

    [PunRPC]
    private void RpcVoteProgress(int agreed, int total, PhotonMessageInfo info)    
    {
        if (info.Sender != PhotonNetwork.MasterClient)
            return;

        voteProgress.OnNext((agreed, total));
    }

    [PunRPC]
    private void RpcOpenVote(int requesterActor, int deadline, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient)
            return;

        PhotonPlayer requester = PhotonNetwork.CurrentRoom.GetPlayer(requesterActor);
        string requesterName = requester != null ? requester.NickName : "누군가";

        isVoteOpen = true;
        this.deadline = deadline;
        hasAnswered = requesterActor == PhotonNetwork.LocalPlayer.ActorNumber
            && PhotonNetwork.CurrentRoom.PlayerCount >= 2;

        voteOpened.OnNext((requesterName, deadline));
    }

    [PunRPC]
    private void RpcCloseVote(int reason, string name, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient)
            return;

        isVoteOpen = false;
        hasAnswered = false;
        voteClosed.OnNext(((VoteCloseReason)reason, name));
    }

    #endregion

    #region 인원변화
    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);
        if (PhotonNetwork.IsMasterClient && masterVoting)
            CloseVote(VoteCloseReason.PlayerChanged);
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (!PhotonNetwork.IsMasterClient || !masterVoting)
            return;

        agreed.Remove(otherPlayer.ActorNumber);
        BroadcastProgress();
        CheckPassed();
    }

    public override void OnMasterClientSwitched(PhotonPlayer newMasterClient)
    {
        base.OnMasterClientSwitched(newMasterClient);
        if (PhotonNetwork.LocalPlayer != newMasterClient)
            return;

        if (Stage == CastleStage.Voting)
            CloseVote(VoteCloseReason.PlayerChanged);
    }
    #endregion

    #region 텔레포트
    #endregion

    #region 내 플레이어
    private Player GetLocalPlayer()
    {
        if (cachedPlayer != null)
            return cachedPlayer;

        if (GameSessionManager.Instance == null)
            return null;
            
        GameObject go = GameSessionManager.Instance.LocalPlayer;
        if (go == null)
            return null;

        cachedPlayer = go.GetComponent<Player>();
        return cachedPlayer;
        
    }

    public bool IsLocalActive()
    {
        Player player = GetLocalPlayer();
        return player != null && player.Character.HP > 0;
    }
    #endregion

#if UNITY_EDITOR
    #region 디버그
    // 플레이 중 컴포넌트 ⋮ 메뉴에서 실행. 투표 통과 후 InCastle에 머문 단계를 되돌린다 (5단계 귀환 전 테스트용)
    [ContextMenu("디버그: 마왕성 단계 Ready로")]
    private void DebugResetStage()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogWarning("[CastleTeleportManager] 방 밖이라 단계 변경 불가 (플레이 중에 실행)");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[CastleTeleportManager] 방장이 아니라 단계 변경 불가 (방장 클라이언트에서 실행)");
            return;
        }

        masterVoting = false;
        agreed.Clear();
        SetStage(CastleStage.Ready);
        Debug.Log($"[CastleTeleportManager] 디버그: 단계 Ready로 (이전 단계 {Stage})");
    }
    #endregion
#endif
}
