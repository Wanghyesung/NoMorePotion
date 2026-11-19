using Enzeun.Runtime;

public sealed class ShakePotionEndEvent : GuideEndEvent
{
    private Hand subscribedHand;

    protected override void OnBegin()
    {
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.OnShake += OnShake;
    }

    protected override void OnStop()
    {
        if (subscribedHand != null)
            subscribedHand.OnShake -= OnShake;
        subscribedHand = null;
    }

    private void OnShake(PotionInstance left, PotionInstance right)
    {
        if (left != null || right != null)
            Complete();
    }
}
