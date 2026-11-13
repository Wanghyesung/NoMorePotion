using Photon.Pun;
using UnityEngine;

/// <summary>
/// 네트워크 오브젝트(몬스터 등) 생성/반납 창구
/// 방 안: 방장이 포톤으로 생성/파괴 → 모든 클라이언트가 PhotonPoolAdapter로 PoolManager 사용
/// 방 밖(에디터 단독 실행): PoolManager로 로컬 생성/반납
///
/// 사용법:
///   1. 모든 클라이언트에서 Spawn 전에: NetPool.Prepare(prefab, 개수)   (Start나 밤 시작 등)
///   2. 방장만:                          NetPool.Spawn(prefab, 위치, 회전)
///   3. 반납:                            NetPool.Despawn(gameObject)    (Release 대신)
///
/// 프리팹 조건:
/// - 루트에 PhotonView 필수 (포톤은 PhotonView 없는 프리팹을 생성하지 않음)
/// - 프리팹 이름이 다른 프리팹과 겹치면 안 됨 (포톤은 이름으로 찾음)
///
/// 주의:
/// - 부모 지정 불가 → 씬 루트에 생성됨
/// - 초기화는 IPoolable.OnGetFromPool에서 (포톤 경로는 ViewID가 들어가고 켜진 뒤 호출)
/// - 포톤 경로의 OnReleaseToPool은 오브젝트가 이미 꺼진 뒤 호출됨 (로컬 Release는 끄기 전)
/// - 오브젝트 상태(체력, 타겟, 사망 등)는 동기화해 주지 않음 → 각 스크립트에서 방장 기준으로 동기화 필요
/// </summary>
public static class NetPool
{
    /// <summary>
    /// 풀 등록 + 미리 생성. 모든 클라이언트에서 Spawn 전에 호출
    /// (다른 클라이언트도 방장의 생성 주문을 받으면 자기 풀에서 꺼내기 때문)
    /// </summary>
    public static void Prepare(GameObject prefab, int prewarmCount)
    {
        if (prefab == null)
            return;

        PhotonPoolAdapter.RegisterPooled(prefab);

        if (PoolManager.Instance != null)
            PoolManager.Instance.Prewarm(prefab, prewarmCount);
    }

    /// <summary>
    /// 생성. 방 안이면 방장만 포톤으로 생성(모든 클라이언트에 생김), 방 밖이면 로컬 풀에서 생성
    /// 방장이 아니면 null (경고 로그) → 호출하는 쪽에서 GameAuthority.HasAuthority로 먼저 거르기
    /// </summary>
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

    /// <summary>
    /// 반납. 포톤으로 만든 오브젝트는 방장만 파괴 주문(모든 클라이언트에서 반납됨)
    /// 방장이 아니면 아무것도 안 함 → 방장 쪽에서 반납되면 자동으로 같이 사라짐
    /// 로컬로 만든 오브젝트(방 밖)는 PoolManager.Release
    /// </summary>
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
