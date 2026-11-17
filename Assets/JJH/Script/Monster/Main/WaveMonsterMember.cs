using Photon.Pun;
using UnityEngine;

using SF = UnityEngine.SerializeField;

public class WaveMonsterMember : MonoBehaviourPun
{
    private MonsterMove mover;
    private MonsterCombat combat;

    public bool IsWave { get; private set; }
    public MonsterCombat Combat => combat;

    /// <summary>가라앉는 중인 시체 제외</summary>
    public bool IsAlive => combat != null && combat.GetData() != null && combat.GetData().HP > 0;

    private void Awake()
    {
        mover = GetComponent<MonsterMove>();
        combat = GetComponent<MonsterCombat>();
    }

    
    /// <summary>
    /// 모든 클라이언트에 웨이브 몬스터와 기본 타겟을 전달하는 함수
    /// </summary>
    /// <param name="defendTarget">기본 타겟 GameObject</param>
    public void MarkAsWave(GameObject defendTarget)
    {
        if (PhotonNetwork.InRoom && photonView.ViewID != 0)
            photonView.RPC(nameof(RpcMarkAsWave), RpcTarget.All, NetTarget.ToViewId(defendTarget));
        else
            ApplyMark(defendTarget);
    }

    [PunRPC]
    private void RpcMarkAsWave(int defendTargetViewId) => ApplyMark(NetTarget.FromViewId(defendTargetViewId));

    private void ApplyMark(GameObject defendTarget)
    {
        IsWave = true;
        if (mover != null) mover.SetDefaultTarget(defendTarget);
        WaveMonsterRegistry.Register(this);
    }

    // 풀로 반납되면 표시 해제 (다음 생애는 표시 없이 시작)
    private void OnDisable()
    {
        IsWave = false;
        WaveMonsterRegistry.Unregister(this);
    }
}
