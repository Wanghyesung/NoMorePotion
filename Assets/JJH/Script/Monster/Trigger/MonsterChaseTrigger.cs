using System.Collections.Generic;
using UnityEngine;
using static JJH_Constants;
using SF = UnityEngine.SerializeField;
public class MonsterChaseTrigger : MonoBehaviour
{
    [SF] private MonsterMove mover;
    private List<GameObject> players;

    private void Awake()
    {
        players = new List<GameObject>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(PLAYER_TAG))
        {
            if(mover.nowTarget == null)
                mover.nowTarget = other.gameObject;
            if(!players.Contains(other.gameObject))
                players.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!players.Remove(other.gameObject)) return;
        
        // 죽거나 비활성화된 플레이어 제거
        players.RemoveAll(p => p == null || !p.activeInHierarchy);

        if (mover.nowTarget != null && players.Contains(mover.nowTarget)) return;

        // 타겟이 나갔거나 죽었으면 재선정, 아무도 없으면 해제
        mover.nowTarget = players.Count > 0 ? 
            players[Random.Range(0, players.Count)] : null;
    }
}
