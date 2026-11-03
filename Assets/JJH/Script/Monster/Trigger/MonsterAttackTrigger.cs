using System.Collections.Generic;
using UnityEngine;
using static JJH_Constants;
using static CommonStruct;

using SF = UnityEngine.SerializeField;

public class MonsterAttackTrigger : MonoBehaviour
{
    [SF] private MonsterCombat combat;
    private bool isAlreadyTrigger;
    private void OnEnable()
    {
        isAlreadyTrigger = false;
    }

    private void OnDisable()
    {
        isAlreadyTrigger = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(PLAYER_TAG))
        {
            if (isAlreadyTrigger) return;

            // 이후 플레이어가 생기면 대미지 저항까지 계산하도록 수정
            if(other.TryGetComponent(out IDamageable target)){
                target.TakeDamage(
                    new AttackInfo
                    {
                        Damage = combat.GetDamage(),
                        Position = combat.transform.position
                    });

                    isAlreadyTrigger = true;
            }
        }
    }
}
