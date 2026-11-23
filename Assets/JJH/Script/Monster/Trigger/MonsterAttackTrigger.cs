using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;
using static CommonStruct;
using static JJH_Constants;
using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(Collider))]
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
        if (!IsHitTrigger(other.gameObject)) return;
        if (!other.TryGetComponent(out IDamageable target)) return;
        if (!hitTargets.Add(target)) return;

        target.TakeDamage(GetAttackInfo(), (IAttacker)combat.GetData());
    }

    
    /// <summary>
    /// 플레이어는 본인 클라이언트가, 소유자 없는 대상은 방장이 판정 (임시 )
    /// </summary>
    /// <param name="target">판정 대상</param>
    /// <returns>판정할 경우 : true</returns>
    public static bool IsHitTrigger(GameObject target)
    {
        // 오프라인은 전부 판정
        if (!PhotonNetwork.InRoom) 
            return true;   

        if (target.TryGetComponent(out PlayerRegistryMember player) && player.View != null)
            return player.View.IsMine;

        return GameAuthority.HasAuthority;
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
