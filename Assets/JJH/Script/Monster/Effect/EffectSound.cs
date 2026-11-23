using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using SF = UnityEngine.SerializeField;
using Random = UnityEngine.Random;

public class EffectSound : MonoBehaviour
{
    [SF] private float startDelay;
    [SF] private bool randomVolume;
    [SF] private float minVolume = 0.8f;
    [SF] private float maxVolume = 1f;

    private AudioSource source;
    private float baseVolume;
    private CancellationTokenSource cts;

    private void Awake()
    {
        if(source == null)
            source = GetComponent<AudioSource>();
        source.playOnAwake = false;   // 중복 재생 방지
        baseVolume = source.volume;
    }

    private void OnEnable()
    {
        cts = new CancellationTokenSource();
        PlayAfterDelay(cts.Token).Forget();
    }

    private void OnDisable()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private async UniTaskVoid PlayAfterDelay(CancellationToken token)
    {
        if (startDelay > 0f &&
            await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: token)
                .SuppressCancellationThrow())
            return;

        source.volume = randomVolume ? Random.Range(minVolume, maxVolume) : baseVolume;
        source.PlayOneShot(source.clip);
    }
}
