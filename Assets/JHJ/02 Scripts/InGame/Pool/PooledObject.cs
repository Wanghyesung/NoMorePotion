using System;
using UnityEngine;

/// <summary>
/// 풀에서 나왔다는 표시 PoolManager가 생성 시 자동으로 붙임 → 프리팹에 직접 붙이지 말 것
/// 출신 프리팹(반납할 풀 찾기용), 풀 안 여부(두 번 반납 막기용), IPoolable 목록(캐싱)을 가짐
/// </summary>
public class PooledObject : MonoBehaviour
{
    public GameObject OriginalPrefab {  get; private set; }
    public bool IsInPool { get; private set; }


    private IPoolable[] poolables;

    // ── 아래 함수들은 PoolManager 전용. 다른 곳에서 호출하지 말 것 ──

    /// 생성 직후 1회. 프리팹에 이미 PooledObject가 있어도 복사된 값은 믿을 수 없어서 항상 덮어씀
    public void Setup(GameObject prefab)
    {
        OriginalPrefab = prefab;
        IsInPool = true;
        poolables = GetComponentsInChildren<IPoolable>(true); // 꺼진 자식 포함, 1회 캐싱
    }

    public void NotifyGet()
    {
        IsInPool = false;
        foreach(IPoolable p in poolables)
        {
            try
            {
                p.OnGetFromPool();
            }
            catch(Exception e)
            {
                // 한 컴포넌트가 실패해도 나머지는 계속 알림
                Debug.LogException(e, this);
            }
        }
    }

    public void NotifyRelease()
    {
        IsInPool = true;
        foreach(IPoolable p in poolables)
        {
            try
            {
                p.OnReleaseToPool();
            }
            catch(Exception e)
            {
                // 한 컴포넌트가 실패해도 나머지는 계속 알림
                Debug.LogException(e, this);
            }
        }
    }
}
