using Enzeun.Runtime;
using UnityEngine;

// 실습 중에 던진 포션을 착탄 후 다시 손에 든다.
[DisallowMultipleComponent]
public sealed class GuidePotionRefill : MonoBehaviour
{
    private GuideEndEvent condition;
    private Hand hand;

    public void Begin()
    {
        condition = GetComponent<GuideEndEvent>();
        hand = GameSessionManager.Instance.LocalPlayer.GetComponent<Hand>();
        hand.OnThrownPotionLanded += ReturnToHand;
    }

    private void ReturnToHand(PotionInstance potion)
    {
        if (hand != null && condition.IsListening)
            hand.TryHoldPotion(potion);
    }

    private void OnDisable()
    {
        if (hand != null)
            hand.OnThrownPotionLanded -= ReturnToHand;
        hand = null;
    }
}
