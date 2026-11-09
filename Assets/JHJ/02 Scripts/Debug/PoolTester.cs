using System.Collections.Generic;
using UnityEngine;

// 테스트용 (커밋 안 함): 컴포넌트 ⋮ 메뉴로 PoolManager 기능 실행
public class PoolTester : MonoBehaviour
{
    [SerializeField] private GameObject plainPrefab;      // IPoolable 없는 큐브
    [SerializeField] private GameObject poolablePrefab;   // PoolableLogger 붙은 구
    [SerializeField] private int prewarmCount = 10;
    [SerializeField] private float spawnRange = 5f;

    private readonly List<GameObject> spawned = new List<GameObject>();

    [ContextMenu("1. Poolable 3개 꺼내기")]
    private void GetPoolable3() => GetMany(poolablePrefab, 3);

    [ContextMenu("2. Plain 3개 꺼내기")]
    private void GetPlain3() => GetMany(plainPrefab, 3);

    [ContextMenu("3. 전부 반납")]
    private void ReleaseAll()
    {
        if (!CheckManager()) return;

        foreach (GameObject go in spawned)
            PoolManager.Instance.Release(go);

        Debug.Log($"[PoolTest] ── {spawned.Count}개 반납 ──");
        spawned.Clear();
    }

    [ContextMenu("4. 마지막 것 두 번 반납")]
    private void ReleaseLastTwice()
    {
        if (!CheckManager()) return;
        if (spawned.Count == 0)
        {
            Debug.LogWarning("[PoolTest] 꺼낸 오브젝트 없음. 먼저 1번 실행");
            return;
        }

        GameObject last = spawned[spawned.Count - 1];
        spawned.RemoveAt(spawned.Count - 1);   // 목록에서 빼서 3번에서 다시 반납되지 않게

        Debug.Log("[PoolTest] ── 같은 오브젝트 두 번 반납 ──");
        PoolManager.Instance.Release(last);
        PoolManager.Instance.Release(last);    // 여기서 경고가 떠야 함
    }

    [ContextMenu("5. 풀 아닌 오브젝트 반납")]
    private void ReleaseNotPooled()
    {
        if (!CheckManager()) return;

        GameObject go = Instantiate(plainPrefab, transform.position + Vector3.up * 2f, Quaternion.identity);
        go.name = "NotPooled";

        Debug.Log("[PoolTest] ── 풀 아닌 오브젝트 반납 ──");
        PoolManager.Instance.Release(go);      // 경고 + Destroy 되어야 함
    }

    [ContextMenu("6. Poolable Prewarm")]
    private void PrewarmPoolable()
    {
        if (!CheckManager()) return;

        Debug.Log($"[PoolTest] ── Prewarm {prewarmCount}개 ──");
        PoolManager.Instance.Prewarm(poolablePrefab, prewarmCount);
        LogStorageCount();
    }

    [ContextMenu("7. 보관 개수 출력")]
    private void LogStorageCount()
    {
        if (!CheckManager()) return;

        Transform storage = PoolManager.Instance.transform.Find("Storage");
        Debug.Log($"[PoolTest] Storage 보관 개수: {(storage != null ? storage.childCount : -1)}, 꺼낸 개수: {spawned.Count}");
    }

    private void GetMany(GameObject prefab, int count)
    {
        if (!CheckManager()) return;

        Debug.Log($"[PoolTest] ── {prefab.name} {count}개 꺼내기 ──");
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRange;
            Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);

            GameObject go = PoolManager.Instance.Get(prefab, position, Quaternion.identity);
            if (go != null)
                spawned.Add(go);
        }
    }

    private bool CheckManager()
    {
        if (PoolManager.Instance != null) return true;
        Debug.LogWarning("[PoolTest] PoolManager 없음");
        return false;
    }
}