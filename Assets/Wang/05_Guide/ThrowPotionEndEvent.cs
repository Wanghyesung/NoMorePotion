public sealed class ThrowPotionEndEvent : GuideEndEvent
{
    private Hand subscribedHand;

    protected override void OnBegin()
    {
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.OnPotionThrown += Complete;
    }

    protected override void OnStop()
    {
        if (subscribedHand != null)
            subscribedHand.OnPotionThrown -= Complete;
        subscribedHand = null;
    }
}
