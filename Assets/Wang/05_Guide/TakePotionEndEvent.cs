public sealed class TakePotionEndEvent : GuideEndEvent
{
    private Hand subscribedHand;

    protected override void OnBegin()
    {
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.OnPotionHeld += Complete;
    }

    protected override void OnStop()
    {
        if (subscribedHand != null)
            subscribedHand.OnPotionHeld -= Complete;
        subscribedHand = null;
    }
}
