using UnityEngine;

public sealed class HealEndEvent : GuideEndEvent
{
    [SerializeField, Min(1)] private int missingHealth = 600;
    [SerializeField] private KeyGuide usePotionKey = KeyGuide.MouseLeftHold;
    private Player subscribedPlayer;
    private Hand subscribedHand;

    protected override void OnBegin()
    {
        subscribedPlayer = GameSessionManager.Instance.LocalPlayer.GetComponent<Player>();
        PlayerCharacter character = subscribedPlayer.Character;
        character.HP = Mathf.Min(character.HP, Mathf.Max(1, character.MaxHP - missingHealth));
        subscribedPlayer.OnHealed += Complete;
        subscribedHand = subscribedPlayer.GetComponent<Hand>();
        subscribedHand.OnPotionHeld += UpdateGuideKey;
        subscribedHand.OnPotionDrunk += UpdateGuideKey;
        subscribedHand.OnPotionThrown += UpdateGuideKey;
        UpdateGuideKey();
    }

    private void UpdateGuideKey()
    {
        bool hasPotion = !subscribedHand.IsBothHandsEmpty;
        KeyCanvas.Instance.ShowPlayerKey(hasPotion ? usePotionKey : GuideKey, hasPotion ? 2 : 1);
    }

    protected override void OnStop()
    {
        if (subscribedPlayer != null)
            subscribedPlayer.OnHealed -= Complete;
        if (subscribedHand != null)
        {
            subscribedHand.OnPotionHeld -= UpdateGuideKey;
            subscribedHand.OnPotionDrunk -= UpdateGuideKey;
            subscribedHand.OnPotionThrown -= UpdateGuideKey;
        }
        subscribedPlayer = null;
        subscribedHand = null;
    }
}
