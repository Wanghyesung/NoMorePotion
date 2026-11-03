using System.Collections.Generic;
using UnityEngine;

using SF = UnityEngine.SerializeField;
[RequireComponent(typeof(MonsterAnimationController))]

[RequireComponent(typeof(MonsterMove))]
public class MonsterCombat : MonoBehaviour
{
    [SF] private MonsterAnimationController anim;
    [SF] private MonsterMove mover;
    [SF] private Collider biteHitbox;
    [SF] private NormalMonster data;

    private void Awake()
    {
        anim = GetComponent<MonsterAnimationController>();
        mover = GetComponent<MonsterMove>();
    }

    private void Init(string name,int hp, Dictionary<DamageType, int> immune,float speed,
        int id, int instanceId, string spawnArea, string description, DamageType damageType)
    {
        data = new(name, hp, immune, speed, id, instanceId, spawnArea, description, damageType);
        data.OnDead += MonsterDead;
    }

    #region
    public int GetDamage()
    {
        // 테스트용
        return 10;
        //return data.AtkPoint;
    }

    public DamageType GetDamageType()
    {
        return data.DamageType;
    }
    #endregion

    #region 전투 애니메이션 용
    private void NormalAttackAnimationStart()
    {
        mover.IsAnimation = true;
    }

    private void NormalAttackOn()
    {
        biteHitbox.gameObject.SetActive(true);
    }

    private void NormalAttackOff()
    {
        biteHitbox.gameObject.SetActive(false);
    }
    private void NormalAttackAnimationEnd()
    {
        mover.IsAnimation = false;
        anim.IsNormalAttack(false);
    }

    private void MonsterDead()
    {
        anim.IsDead();
    }
    #endregion
}
