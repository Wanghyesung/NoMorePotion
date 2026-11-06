using UnityEngine;

using static JJH_Constants;

using SF = UnityEngine.SerializeField;

[RequireComponent(typeof(Animator))]
public class MonsterAnimationController : MonoBehaviour
{
    private static readonly int MoveSpeedHash = Animator.StringToHash(ANIMATOR_MOVE_SPEED);
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

    /// <summary>
    /// 이동 관련 애니메이션(run / walk) 속도 변경 함수
    /// </summary>
    /// <param name="val"> 요청 배율, 기본값 1</param>
    public void SetMoveAnimationSpeed(float val = 1f)
    {
        animator.SetFloat(MoveSpeedHash, val);
    }

    /// <summary>
    /// 공격에 패턴이 있는 경우(ex. 스킬) 실행
    /// </summary>
    /// <param name="pattern"></param>
    public void PlayAttack(AttackPattern pattern)
    {
        animator.SetBool(pattern.NameHash, true);
    }
    /// <summary>
    /// 스킬 종료 제어
    /// </summary>
    /// <param name="skillName"></param>
    /// <param name="value"></param>
    public void SetSkill(string skillName, bool value)
    {
        animator.SetBool(skillName, value);
    }
}
