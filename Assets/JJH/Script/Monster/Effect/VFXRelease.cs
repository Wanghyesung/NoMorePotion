using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.VFX;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(VisualEffect))]
public class VFXRelease : MonoBehaviour
{
    // 풀 반납 대기 시간
    [SF] private float lifetime = 2.5f;

    private VisualEffect vfx;
    private CancellationTokenSource cts;

    private void Awake()
    {
        if(vfx == null)
            vfx = GetComponent<VisualEffect>();
    }

    private void OnEnable()
    {
        vfx.Reinit();
        vfx.Play();

        cts = new CancellationTokenSource();
        ReleaseAfter(cts.Token).Forget();
    }
    private void OnDisable()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private async UniTaskVoid ReleaseAfter(CancellationToken token)
    {
        if (await UniTask.Delay(TimeSpan.FromSeconds(lifetime), cancellationToken: token)
            .SuppressCancellationThrow())
            return;

        PoolManager.Instance.Release(gameObject);
    }

}
