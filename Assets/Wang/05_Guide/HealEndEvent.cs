using UnityEngine;

public sealed class HealEndEvent : GuideEndEvent
{
    [SerializeField, Min(1)] private int missingHealth = 600;
    private Player subscribedPlayer;

    protected override void OnBegin()
    {
        subscribedPlayer = GameSessionManager.Instance.LocalPlayer.GetComponent<Player>();
        PlayerCharacter character = subscribedPlayer.Character;
        character.HP = Mathf.Min(character.HP, Mathf.Max(1, character.MaxHP - missingHealth));
        subscribedPlayer.OnHealed += Complete;
    }

    protected override void OnStop()
    {
        if (subscribedPlayer != null)
            subscribedPlayer.OnHealed -= Complete;
        subscribedPlayer = null;
    }
}
