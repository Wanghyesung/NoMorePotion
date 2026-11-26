using System;
using Photon.Pun;
using UnityEngine;
using static JJH_Enums;

using SF = UnityEngine.SerializeField;

/// <summary>
/// 스폰 지점 하나 (위치 + 생성할 몬스터)
/// </summary>
[Serializable]
public class FieldSpawnSlot
{
    public Transform Point;
    public GameObject Prefab;
    #region 지역보스 / 진희진
    public bool IsBoss;             // 체크하면 지역보스
    public SpawnAreaType Area;      // 보스일때만 사용
    #endregion
}

public class FieldMonsterSpawner : MonoBehaviourPunCallbacks
{
    [SF] private FieldSpawnSlot[] slots = new FieldSpawnSlot[6];
    private bool registered;
    private bool spawned;

    private void Awake()
    {
        RegisterPrefabs();
    }

    private void RegisterPrefabs()
    {
        if (registered) return;
        registered = true;

        foreach (FieldSpawnSlot slot in slots)
        {
            if (slot == null || slot.Prefab == null) continue;
            PhotonPoolAdapter.RegisterPooled(slot.Prefab);
        }
    }


    private void Start()
    {
        TrySpawnAll();
    }

    /// <summary>
    /// 방에 들어간 순간 다시 시도 (씬이 방 입장 전에 열린 경우)
    /// </summary>
    public override void OnJoinedRoom()
    {
        TrySpawnAll();
    }

    private void TrySpawnAll()
    {
        if (spawned) return;

        // 서버에는 연결됐는데 아직 방에 안 들어간 상태
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom) return;
        if (!GameAuthority.HasAuthority) return;

        spawned = true;

        int count = 0;
        foreach (FieldSpawnSlot slot in slots)
        {
            if (slot == null || slot.Point == null || slot.Prefab == null) continue;

            #region 지역보스 / 진희진
            if (slot.IsBoss && RoomProps.IsAreaBossCleared(PhotonNetwork.CurrentRoom, slot.Area))
            {
                Debug.Log($"[FieldMonsterSpawner] 클리어된 지역보스 생성 안함 : {slot.Area}");
                continue;
            }
            #endregion

            if (NetPool.Spawn(slot.Prefab, slot.Point.position, slot.Point.rotation) != null)
                count++;
        }

        Debug.Log($"[FieldMonsterSpawner] 필드 몬스터 {count}마리 생성", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (slots == null) return;

        foreach (FieldSpawnSlot slot in slots)
        {
            if (slot == null || slot.Point == null) continue;

            Gizmos.color = slot.Prefab != null ? Color.red : Color.gray;   // 프리팹 없는 지점은 회색
            Gizmos.DrawWireSphere(slot.Point.position, 0.5f);
            Gizmos.DrawRay(slot.Point.position, slot.Point.forward);      // 바라볼 방향

            if (slot.Prefab != null)
                UnityEditor.Handles.Label(slot.Point.position + Vector3.up, slot.Prefab.name);
        }
    }
#endif
}
