using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class UsePotionEndEvent : GuideEndEvent
{
    [SerializeField, Min(0f)] private float observeSeconds = 6f;
    private Hand subscribedHand;
    private CancellationTokenSource observeCancellation;

    protected override void OnBegin()
    {
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.OnPotionThrown += OnUsed;
        subscribedHand.OnPotionDrunk += OnUsed;
    }

    private void OnUsed()
    {
        if (observeCancellation != null)
            return;

        HideGuideKey();
        observeCancellation = new CancellationTokenSource();
        ObserveEffect(observeCancellation.Token).Forget();
    }

    private async UniTask ObserveEffect(CancellationToken token)
    {
        // 이동 입력은 유지해서 함정의 범위에서 벗어날 수 있게 합니다.
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(observeSeconds),
            cancellationToken: token).SuppressCancellationThrow();
        if (!canceled)
            Complete();
    }

    protected override void OnStop()
    {
        observeCancellation?.Cancel();
        observeCancellation?.Dispose();
        observeCancellation = null;
        if (subscribedHand != null)
        {
            subscribedHand.OnPotionThrown -= OnUsed;
            subscribedHand.OnPotionDrunk -= OnUsed;
        }
        subscribedHand = null;
    }
}
