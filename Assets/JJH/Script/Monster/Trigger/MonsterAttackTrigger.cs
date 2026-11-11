using System.Collections.Generic;
using UnityEngine;
using static JJH_Constants;
using static CommonStruct;

using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(BoxCollider))]
public class MonsterAttackTrigger : MonoBehaviour
{
    [SF] protected MonsterCombat combat;
    private readonly HashSet<IDamageable> hitTargets = new();

    private void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<MonsterCombat>();
    }

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
        if (!(other.CompareTag(PLAYER_TAG) || other.CompareTag(CORE_TAG))) return;
        if (!AttackTarget.IsValid(other.gameObject)) return;
        if (!other.TryGetComponent(out IDamageable target)) return;
        if (!hitTargets.Add(target)) return;

        target.TakeDamage(GetAttackInfo(), (IAttacker)combat.GetData());
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
