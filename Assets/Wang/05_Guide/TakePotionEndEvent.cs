using UnityEngine;

public sealed class TakePotionEndEvent : GuideEndEvent
{
    [Tooltip("가이드를 진행할 로컬 플레이어의 Hand를 연결하세요.")]
    [SerializeField] private Hand hand;
    private Hand subscribedHand;

    public void SetHand(Hand target)
    {
        bool listening = IsListening;
        if (listening)
            OnStop();
        hand = target;
        if (listening)
            OnBegin();
    }

    protected override void OnBegin()
    {
        subscribedHand = hand;
        subscribedHand.OnPotionHeld += OnPotionHeld;
    }

    protected override void OnStop()
    {
        if (subscribedHand != null)
            subscribedHand.OnPotionHeld -= OnPotionHeld;
        subscribedHand = null;
    }

    private void OnPotionHeld() => Complete();
}
