public sealed class DrinkPotionEndEvent : GuideEndEvent
{
    private Hand subscribedHand;

    protected override void OnBegin()
    {
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.OnPotionDrunk += Complete;
    }

    protected override void OnStop()
    {
        if (subscribedHand != null)
            subscribedHand.OnPotionDrunk -= Complete;
        subscribedHand = null;
    }
}
