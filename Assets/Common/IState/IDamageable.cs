using UnityEngine;
using static CommonStruct;

public interface IDamageable
{
    public void TakeDamage(AttackInfo attackInfo);
    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker);
    public void TakeDamage(AttackInfo attackInfo, Character attacker);
}
