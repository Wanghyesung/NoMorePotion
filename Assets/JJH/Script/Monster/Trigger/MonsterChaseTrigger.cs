using System.Collections.Generic;
using UnityEngine;
using static JJH_Constants;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(SphereCollider))]
public class MonsterChaseTrigger : MonoBehaviour
{
    [SF] private MonsterMove mover;
    private readonly List<GameObject> players = new();
    private readonly List<GameObject> candidates = new();

    private void Awake()
    {
        if (mover == null)
            mover = GetComponentInParent<MonsterMove>();
    }

    private void Update()
    {

        // 타겟이 없거나 수정구슬을 노리는 경우
        if (players.Count == 0) return;
        if (mover.nowTarget != null && !mover.IsTargetObjective) return;

        players.RemoveAll(p => p == null || !p.activeInHierarchy);
        GameObject pick = PickTarget();
        if (pick != null) mover.SetTarget(pick);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(PLAYER_TAG)) return;

        if (!players.Contains(other.gameObject))
            players.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!players.Remove(other.gameObject)) return;

        // 죽었거나 비활성화된 플레이어 먼저 정리
        players.RemoveAll(p => p == null || !p.activeInHierarchy);

        // 도발 중엔 타겟 유지
        if (mover.IsProvoked) return;  
        // 현재 타겟이 아직 범위 안에 살아있으면 유지
        if (mover.nowTarget != null && players.Contains(mover.nowTarget)) return;

        // 타겟이 나갔거나 죽었으면 재선정 없으면 해제
        mover.SetTarget(PickTarget());
    }

    // 재추적 쿨다운 중인 플레이어는 제외하고 무작위 선택
    private GameObject PickTarget()
    {
        candidates.Clear();

        foreach (GameObject p in players)
        {
            bool can = mover.CanAcquireTarget(p);
            if (can) candidates.Add(p);
        }

        return candidates.Count > 0
            ? candidates[Random.Range(0, candidates.Count)]
            : null;
    }
}
