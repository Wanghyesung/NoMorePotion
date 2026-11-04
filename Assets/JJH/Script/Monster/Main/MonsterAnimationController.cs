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

    public void AnimatorRebind()
    {
        animator.Rebind();
    }

    /// <summary>
    /// 걷기 애니메이션 재생 및 정지 함수
    /// </summary>
    /// <param name="type"></param>
    public void IsWalk(bool type = true)
    {
        animator.SetBool(IsWalkHash, type);
    }


    /// <summary>
    /// 뛰기 애니메이션 재생 및 정지 함수
    /// </summary>
    /// <param name="type"></param>
    public void IsRun(bool type = true) 
    {
        animator.SetBool(IsRunHash, type);
    }

    /// <summary>
    /// 통상 공격 재생 및 정지 함수
    /// </summary>
    /// <param name="type"></param>
    public void IsNormalAttack(bool type = true)
    {
        animator.SetBool(IsNomalAttackHash, type);
    }

    /// <summary>
    /// 사망 애니메이션 재생 및 정지 함수
    /// </summary>
    /// <param name="type"></param>
    public void IsDead(bool type = true)
    {
        animator.SetBool(IsDeadHash, type);
    }
}
