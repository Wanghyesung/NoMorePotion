using Photon.Pun;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static JJH_Constants;
using static JJH_Enums;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(SphereCollider))]
public class MonsterChaseTrigger : MonoBehaviour
{
    [SF] private MonsterMove mover;
    [SF] private bool randomAggroActive = true;
    [SF] private float randomAggroLockTime = 10f;
    [SF] private SphereCollider chaseCollider;


    [Header("후공 여부")]
    [SF] private bool passive;
    private readonly List<GameObject> players = new();
    private readonly List<GameObject> candidates = new();

    private float tickTimer;
    private int targetTickCount;

    private void Awake()
    {
        if (mover == null)
            mover = GetComponentInParent<MonsterMove>();
        if (chaseCollider == null && TryGetComponent(out SphereCollider col))
            chaseCollider = col;
    }

    private void OnEnable()
    {
        tickTimer = Random.Range(0f, FSM_TIMER / MIL_SEC_F);
    }

    private void Update()
    {
        if (PhotonNetwork.InRoom && !GameAuthority.HasAuthority) return;
        if (mover.state.GetStateType() == MonsterState.Death) return;
        tickTimer += Time.deltaTime;
        if (tickTimer < FSM_TIMER / MIL_SEC_F) return;
        tickTimer = 0f;
        targetTickCount++;

        ClearOuterPlayer();

        // 타겟이 없거나 수정구슬을 노리는 경우
        if (players.Count == 0) return;
        GameObject pick;
        if (mover.nowTarget != null && !mover.IsTargetObjective)
        {
            if (targetTickCount > TARGET_CHANGE_TICK && randomAggroActive)
            {
                pick = PickTarget(mover.nowTarget);
                if (pick != null)
                    mover.ForceTarget(pick, randomAggroLockTime);
                targetTickCount = 0;
                return;
            }
            else return;
        }

        // 후공일 경우 건너 뜀
        if (passive) return;

        players.RemoveAll(p => p == null || !p.activeInHierarchy);
                
        pick = PickTarget();
        if (pick != null) 
            mover.SetTarget(pick);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!(other.CompareTag(PLAYER_TAG) || other.CompareTag(CORE_TAG))) return;

        if (!players.Contains(other.gameObject))
            players.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (mover.state.GetStateType() == MonsterState.Death) return;
        if (!players.Remove(other.gameObject)) return;

        // 죽었거나 비활성화된 플레이어 먼저 정리
        players.RemoveAll(p => p == null || !p.activeInHierarchy);

        // 후공 상태면 아무것도 안함
        if (passive && mover.nowTarget == null) return;

        // 도발 중엔 타겟 유지
        if (mover.IsProvoked) return;  
        // 현재 타겟이 아직 범위 안에 살아있으면 유지
        if (mover.nowTarget != null && players.Contains(mover.nowTarget)) return;

        // 타겟이 나갔거나 죽었으면 재선정 없으면 해제
        mover.SetTarget(PickTarget());
    }

    /// <summary>
    /// 재추적 쿨다운 중인 플레이어는 제외하고 무작위 선택
    /// <para>제외할 타겟이 있으면 목록에서 빼고 뽑고, 다른 후보가 없으면 그 타겟 유지</para>
    /// </summary>
    /// <param name="exclude">제외할 타겟</param>
    /// <returns>지정된 타겟</returns>
    private GameObject PickTarget(GameObject exclude = null)
    {
        candidates.Clear();

        foreach (GameObject p in players)
        {
            if (p == exclude || !mover.CanAcquireTarget(p)) continue;
            candidates.Add(p);
        }

        // 바꿀 후보가 없으면 기존 타겟 유지
        if (candidates.Count == 0 && exclude != null && mover.CanAcquireTarget(exclude))
            return exclude;

        return candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : null;
    }

    /// <summary>
    /// 범위 외부에 있는 플레이어 명시적 정리
    /// </summary>
    private void ClearOuterPlayer()
    {
        float radius = chaseCollider.radius * transform.lossyScale.x;
        float sqr = radius * radius;
        Vector3 center = chaseCollider.transform.TransformPoint(chaseCollider.center);

        players.RemoveAll(p => p == null || !p.activeInHierarchy ||
                               (p.transform.position - center).sqrMagnitude > sqr);
    }
}
