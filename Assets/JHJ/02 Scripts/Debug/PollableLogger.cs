using UnityEngine;

// 테스트용 (커밋 안 함): 풀 생명주기 순서·위치·재사용 여부 확인
public class PoolableLogger : MonoBehaviour, IPoolable
{
    private void Awake() => Log("Awake");
    private void OnEnable() => Log("OnEnable");
    private void OnDisable() => Log("OnDisable");

    public void OnGetFromPool() => Log("OnGetFromPool");
    public void OnReleaseToPool() => Log($"OnReleaseToPool (켜진 상태: {gameObject.activeInHierarchy})");

    // InstanceID로 같은 오브젝트가 재사용됐는지 구분
    private void Log(string evt)
    {
        Debug.Log($"[PoolTest] {name} #{GetInstanceID()} | {evt} | 위치 {transform.position}", this);
    }
}