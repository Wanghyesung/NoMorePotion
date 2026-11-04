using System.Collections.Generic;
using UnityEngine;
using static JJH_Constants;
using static CommonStruct;

using SF = UnityEngine.SerializeField;

public class MonsterAttackTrigger : MonoBehaviour
{
    [SF] private MonsterCombat combat;
    private readonly HashSet<IDamageable> hitTargets = new();

    private void OnEnable() 
    {
        hitTargets.Clear();
    }

    private void OnDisable() 
    { 
        hitTargets.Clear(); 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(PLAYER_TAG)) return;
        if (!other.TryGetComponent(out IDamageable target)) return;
        if (!hitTargets.Add(target)) return;

        target.TakeDamage(GetAttackInfo());
    }

    protected virtual AttackInfo GetAttackInfo()
    {
        return new AttackInfo
        {
            Damage = combat.GetDamage(),
            Position = combat.transform.position
        };
    }
}
