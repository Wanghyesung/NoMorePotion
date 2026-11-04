using UnityEngine;
using SF = UnityEngine.SerializeField;
public class WolfCombat : MonsterCombat
{
    [SF] private Collider poundHitbox;
    private void PoundAttackAnimationStart()
    {
        mover.IsAnimation = true;
    }

    private void PoundAttackOn()
    {
        poundHitbox.gameObject.SetActive(true);
    }

    private void PoundAttackOff()
    {
        poundHitbox.gameObject.SetActive(false);
    }

    private void PoundAttackAnimationEnd()
    {
        mover.IsAnimation = false;
        anim.IsNormalAttack(false);
    }
}
