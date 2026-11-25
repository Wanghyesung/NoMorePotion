using System.Collections.Generic;
using UnityEngine;

// 공유하는 기본값만 보관한다. 현재 HP와 버프 배율은 각 플레이어의 Character에 둔다.
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Player/Stats")]
public sealed class SOPlayerStats : ScriptableObject
{
    [SerializeField] private string playerName = "Player";
    [SerializeField, Min(1)] private int maxHP = 3000;
    [SerializeField, Min(0)] private int attackPower = 1;
    [SerializeField, Min(1)] private int maxAttackPower = 10;

    public PlayerCharacter CreateCharacter() => new PlayerCharacter(
        playerName, maxHP, maxHP, new Dictionary<DamageType, int>(), attackPower, maxAttackPower);
}
