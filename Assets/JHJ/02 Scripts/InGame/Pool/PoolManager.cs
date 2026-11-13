using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 팀 공용 오브젝트 풀
/// 사용법:
///   꺼내기: PoolManager.Instance.Get(prefab, 위치, 회전)       // Instantiate 대신
///   반납:   PoolManager.Instance.Release(gameObject)            // Destroy 대신
///   미리:   PoolManager.Instance.Prewarm(prefab, 개수)
///   반납 토큰: PoolManager.GetReleaseToken(gameObject)          // 반납·파괴 시 취소됨
///     예약·UniTask에 넘기면 반납될 때 같이 취소 (풀 오브젝트는 destroyCancellationToken 대신 이걸)
///     TimeManager.Instance.Schedule(5f, () => { ... }, PoolManager.GetReleaseToken(gameObject));
///     UniTask.Delay(..., cancellationToken: PoolManager.GetReleaseToken(gameObject));
/// - 프리팹에 아무것도 붙일 필요 없음
/// - 재사용 시 Start/Awake는 다시 안 불림 → 초기화는 IPoolable.OnGetFromPool에서
/// </summary>
public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [SerializeField] private int defaultMaxSize = 50;   // 프리팹별 보관 상한. 반납 시 넘으면 파괴 (꺼내기는 제한 없음)

    private Dictionary<GameObject, ObjectPool<PooledObject>> pools = new Dictionary<GameObject, ObjectPool<PooledObject>>();
    private Transform storage;


    private void Awake()
    {
        if(Instance != null)
        {
            Debug.LogWarning("[PoolManager] Instance 이미 있음");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 보관함: 꺼 둔 부모. 이 아래서 Instantiate하면 Awake/OnEnable이 안 돌아서
        // 위치를 세팅한 뒤에 켤 수 있음 (NavMesh 몬스터가 엉뚱한 위치에 붙는 문제 방지)
        GameObject storageObject = new GameObject("Storage");
        storageObject.transform.SetParent(transform, false);
        storageObject.SetActive(false);
        storage = storageObject.transform;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 풀에서 꺼내서 위치 세팅 후 켬. 비어 있으면 새로 생성. parent가 null이면 씬 루트
    /// </summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        PooledObject pooled = RentPooled(prefab, position, rotation, parent);

        if (pooled == null)             
            return null;

        pooled.gameObject.SetActive(true);
        pooled.NotifyGet(); // 켜진 뒤 알림 → OnGetFromPool에서 켜진 상태가 필요한 초기화 가능

        return pooled.gameObject;
    }

    /// <summary>
    /// 켜지 않고 꺼냄. PhotonAdaptor 전용 /  로컬은 Get 사용
    /// </summary>
    public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        PooledObject pooled = RentPooled(prefab, position, rotation, parent);
        return pooled != null ? pooled.gameObject : null;
    }

    /// <summary>
    /// 풀에 반납 (끄고 보관함으로). 풀 출신이 아니면 경고 후 Destroy, 두 번 반납은 무시
    /// </summary>
    public void Release(GameObject target)
    {
        if (target == null) return;

        if (!target.TryGetComponent(out PooledObject pooled) || pooled.OriginalPrefab == null)
        {
            Debug.LogWarning($"[PoolManager] 풀에서 나온 오브젝트가 아님, 반납: {target.name}");
            Destroy(target);
            return;
        }

        // 두 번 반납 검사를 풀 검사보다 먼저: 이미 보관 중인 걸 실수로 Destroy하지 않게
        if (pooled.IsInPool)
        {
            Debug.LogWarning($"[PoolManager] 두 번 반납되어 반납 무시, {target.name}");
            return;
        }

        // 반납은 "내가 준 것만" → 풀이 없으면 새로 만들지 않고 이상 상황으로 처리
        if (!pools.TryGetValue(pooled.OriginalPrefab, out ObjectPool<PooledObject> pool))
        {
            Debug.LogWarning($"[PoolManager] {target.name}의 풀을 찾을 수 없음 → Destroy");
            Destroy(target);
            return;
        }

        pooled.NotifyRelease(); // 끄기 전에 알림 → OnReleaseToPool에서 켜진 상태가 필요한 정리 가능
        target.SetActive(false);
        target.transform.SetParent(storage, false);
        pool.Release(pooled);
    }

    /// <summary>
    /// 이번 사용분이 끝나면(반납·파괴) 취소되는 토큰. 풀 오브젝트의 예약·UniTask를 반납과 같이 끊을 때 사용
    /// (destroyCancellationToken은 파괴될 때만 취소 → 반납돼도 안 끊김)
    /// 풀 출신이 아니면 None (취소되지 않음), 보관 중이면 이미 취소된 토큰
    /// </summary>
    public static CancellationToken GetReleaseToken(GameObject target)
    {
        if (target != null && target.TryGetComponent(out PooledObject pooled))
            return pooled.ReleaseToken;

        return CancellationToken.None;
    }

    /// <summary>
    /// 미리 만들어 보관. 밤 시작 등 몰릴 때 끊김 방지용
    /// 보관 중인 게 이미 있으면 모자란 만큼만 생성 (결과적으로 count개 보관)
    /// </summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] Prewarm: prefab 없음");
            return;
        }
        if (count <= 0)
            return;

        ObjectPool<PooledObject> pool = GetOrCreatePool(prefab);

        // count개를 꺼냈다가 (모자라면 Create) 바로 돌려놓음
        List<PooledObject> temp = new List<PooledObject>(count);
        for (int i = 0; i < count; i++)
            temp.Add(pool.Get());

        foreach (PooledObject pooled in temp)
            pool.Release(pooled);   // maxSize 초과분은 바로 파괴됨

        // PoolManager.Get/Release가 아니라 ObjectPool을 직접 사용 → 켜지 않고 IPoolable 알림도 안 보냄
        // (Create 후 바로 Release하지 않고 Get→Release: ObjectPool의 생성 개수 통계가 어긋나지 않게)
    }

    // ObjectPool은 보관만 담당. 꺼냄/반납 콜백은 일부러 비움
    // → 켜기·알림은 PoolManager.Get/Release에서 직접 (Prewarm은 알림 없이 보관만 해야 해서)
    private ObjectPool<PooledObject> GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out ObjectPool<PooledObject> pool))
            return pool;

        pool = new ObjectPool<PooledObject>(
            createFunc: () => Create(prefab),
            actionOnDestroy: p => Destroy(p.gameObject),
            collectionCheck: false,
            maxSize: defaultMaxSize
            );

        pools.Add(prefab, pool);
        return pool;
    }

    /// ObjectPool의 createFunc. 풀이 비었을 때만 불림 (직접 호출하지 않음)
    private PooledObject Create(GameObject prefab)
    {
        GameObject go = Instantiate(prefab, storage);
        go.SetActive(false);

        // 프리팹에 이미 붙어 있으면 그걸 쓰고 없으면 붙임
        if (!go.TryGetComponent(out PooledObject pooled))
            pooled = go.AddComponent<PooledObject>();

        pooled.Setup(prefab);   // 출신 프리팹 기록 + IPoolable 캐싱
        return pooled;
    }

    private PooledObject RentPooled(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        if(prefab == null)
        {
            Debug.LogError("[PoolManager] 프리팹 없음");
            return null;
        }

        PooledObject pooled = GetOrCreatePool(prefab).Get();

        pooled.transform.SetPositionAndRotation(position, rotation);
        pooled.transform.SetParent(parent, true);

        return pooled;
    }
}
