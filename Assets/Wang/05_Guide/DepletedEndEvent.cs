using Enzeun.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class DepletedEndEvent : GuideEndEvent
{
    [SerializeField] private WorldObject worldObject;
    [Tooltip("포션을 꺼내면 던지기 안내로 바꾸고, 대상 소진은 계속 기다립니다.")]
    [FormerlySerializedAs("hideKeyWhenPotionHeld")]
    [SerializeField] private bool showThrowGuideWhenPotionHeld;
    private WorldObject subscribedObject;
    private Hand subscribedHand;

    private void Reset() => worldObject = GetComponent<WorldObject>();

    protected override void OnBegin()
    {
        subscribedObject = worldObject;
        subscribedObject.OnDepleted += OnDepleted;
        if (showThrowGuideWhenPotionHeld)
        {
            subscribedHand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
            subscribedHand.OnPotionHeld += ShowThrowGuide;
            if (!subscribedHand.IsBothHandsEmpty)
                ShowThrowGuide();
        }
    }

    protected override void OnStop()
    {
        if (subscribedObject != null)
            subscribedObject.OnDepleted -= OnDepleted;
        subscribedObject = null;
        if (subscribedHand != null)
            subscribedHand.OnPotionHeld -= ShowThrowGuide;
        subscribedHand = null;
    }

    private void ShowThrowGuide() => KeyCanvas.Instance.ShowPlayerKey(KeyGuide.ThrowOptions);

    private void OnDepleted(WorldObject target) => Complete();
}
