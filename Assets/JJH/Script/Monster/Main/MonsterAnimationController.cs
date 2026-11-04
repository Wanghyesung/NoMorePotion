using UnityEngine;

using static JJH_Constants;

using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(Animator))]
public class MonsterAnimationController : MonoBehaviour
{
    private static readonly int IsDeadHash = Animator.StringToHash(IS_DEAD);
    private static readonly int IsNomalAttackHash = Animator.StringToHash(IS_NORMAL);
    private static readonly int IsRunHash = Animator.StringToHash(IS_RUN);
    private static readonly int IsWalkHash = Animator.StringToHash(IS_WALK);

    [SF] protected Animator animator;

    private void Awake()
    {
        if(animator == null)
            animator = GetComponent<Animator>();
    }

    public void IsWalk(bool type = true)
    {
        animator.SetBool(IsWalkHash, type);
    }

    public void IsRun(bool type = true) 
    {
        animator.SetBool(IsRunHash, type);
    }

    public void IsNormalAttack(bool type = true)
    {
        animator.SetBool(IsNomalAttackHash, type);
    }

    public void IsDead(bool type = true)
    {
        animator.SetBool(IsDeadHash, type);
    }
}
