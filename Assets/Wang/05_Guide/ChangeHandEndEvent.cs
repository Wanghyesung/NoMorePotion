public sealed class ChangeHandEndEvent : GuideEndEvent
{
    private Hand subscribedHand;

    protected override void OnBegin()
    {
        subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        subscribedHand.OnFocusChanged += OnFocusChanged;
    }

    protected override void OnStop()
    {
        if (subscribedHand != null)
            subscribedHand.OnFocusChanged -= OnFocusChanged;
        subscribedHand = null;
    }

    private void OnFocusChanged(int side) => Complete();
}
