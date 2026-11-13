using Photon.Pun;
using UnityEngine;

/// <summary>
/// 네트워크 오브젝트(몬스터 등) 생성/반납 창구
/// 방 안: 방장이 포톤으로 생성/파괴 → 모든 클라이언트가 PhotonPoolAdapter로 PoolManager 사용
/// 방 밖(에디터 단독 실행): PoolManager로 로컬 생성/반납
/// </summary>
public static class NetPool
{

    public static void Prepare(GameObject prefab, int prewarmCount)
    {
        if (prefab == null)
            return;

        PhotonPoolAdapter.RegisterPooled(prefab);

        if (PoolManager.Instance != null)
            PoolManager.Instance.Prewarm(prefab, prewarmCount);
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            Debug.LogError("[NetPool] prefab이 없음, 스폰 불가");
            return null;
        }

        if (!PhotonNetwork.InRoom)
        {
            PoolManager pool = PoolManager.Instance;
            return pool != null ? pool.Get(prefab, position, rotation) : null;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[NetPool] MasterClient가 아님, 스폰 불가");
            return null;
        }
        
            return PhotonNetwork.InstantiateRoomObject(prefab.name, position, rotation);
    }

    public static void Despawn(GameObject target)
    {
        if (target == null) return;

        // 포톤으로 만든 오브젝트인가
        bool isNetworked = PhotonNetwork.InRoom && target.TryGetComponent(out PhotonView view) && view.InstantiationId > 0;

        if(isNetworked)
        {
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.Destroy(target);

            return;
        }

        if(PoolManager.Instance != null)
            PoolManager.Instance.Release(target);
    }
}
