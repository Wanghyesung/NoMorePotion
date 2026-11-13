using System;
using System.Threading;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// 풀에서 나왔다는 표시 PoolManager가 생성 시 자동으로 붙임 → 프리팹에 직접 붙이지 말 것
/// 출신 프리팹(반납할 풀 찾기용), 풀 안 여부(두 번 반납 막기용), IPoolable 목록(캐싱)을 가짐
/// </summary>
public class PooledObject : MonoBehaviour, IPunInstantiateMagicCallback
{
    public GameObject OriginalPrefab {  get; private set; }
    public bool IsInPool { get; private set; }

    /// <summary>
    /// 이번 사용분 토큰. 반납되거나 파괴되면 취소됨 (꺼낼 때마다 새로 만듦)
    /// AddTo(this)는 파괴될 때만 취소 → 풀 오브젝트는 반납돼도 파괴되지 않아서 이 토큰을 써야 함
    /// 보관 중(IsInPool)에는 이미 취소된 토큰을 돌려줌
    /// </summary>
    public CancellationToken ReleaseToken => useCts != null ? useCts.Token : CancelledToken;

    private static readonly CancellationToken CancelledToken = new CancellationToken(true);

    private IPoolable[] poolables;
    private CancellationTokenSource useCts;

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
        useCts = new CancellationTokenSource(); // OnGetFromPool에서 ReleaseToken을 쓸 수 있게 알림보다 먼저
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
        CancelUse(); // 이번 사용분 예약·UniTask 먼저 정리 → 재사용된 뒤에 옛 예약이 실행되지 않음
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

    private void OnDestroy()
    {
        // 풀 maxSize 초과, 씬 종료 등으로 사용 중에 파괴될 때도 정리
        CancelUse();
    }

    private void CancelUse()
    {
        if (useCts == null) return;

        // Dispose하지 않음: 이미 토큰을 받아 간 쪽이 취소 여부를 계속 확인할 수 있게 (타이머 없는 CTS라 GC로 충분)
        CancellationTokenSource cts = useCts;
        useCts = null;
        try
        {
            cts.Cancel();
        }
        catch (Exception e)
        {
            // 취소 콜백 하나가 실패해도 반납은 계속
            Debug.LogException(e, this);
        }
    }

    // 포톤 호출
    public void OnPhotonInstantiate(PhotonMessageInfo info)
    {
        NotifyGet();
    }
}
