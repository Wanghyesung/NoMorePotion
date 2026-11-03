using UnityEngine;

using static JJH_Constants;

using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(Animator))]
public class MonsterAnimationController : MonoBehaviour
{
    private static readonly int IsRunHash = Animator.StringToHash(IS_RUN);
    private static readonly int IsWalkHash = Animator.StringToHash(IS_WALK);

    [SF] private Animator animator;

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
}
