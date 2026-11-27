using UnityEngine;

using static JJH_Constants;

using SF = UnityEngine.SerializeField;

public class MonsterAnimationController : MonoBehaviour
{
    private static readonly int CounteredHash = Animator.StringToHash(COUNTERED);
    private static readonly int MoveSpeedHash = Animator.StringToHash(ANIMATOR_MOVE_SPEED);
    private static readonly int IsDeadHash = Animator.StringToHash(IS_DEAD);
    private static readonly int IsNomalAttackHash = Animator.StringToHash(IS_NORMAL);
    private static readonly int IsRunHash = Animator.StringToHash(IS_RUN);
    private static readonly int IsWalkHash = Animator.StringToHash(IS_WALK);

    private static readonly int[] SkillHashes = CreateSkillHashes();
    [SF] protected Animator animator;

    private int activeSkillHash;

    private void Awake()
    {
        if(animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private static int[] CreateSkillHashes()
    {
        var hashes = new int[MAX_SKILL_SLOT];
        for (int i = 0; i < MAX_SKILL_SLOT; i++)
            hashes[i] = Animator.StringToHash($"{SKILL_PARAM_PREFIX}{i + 1}");
        return hashes;
    }

    /// <summary>
    /// 칸 번호 → Animator Bool 해시 (범위 밖이면 0)
    /// </summary>
    public static int GetSkillHash(int skillNum) =>
        skillNum >= 1 && skillNum <= MAX_SKILL_SLOT ? SkillHashes[skillNum - 1] : 0;

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
    /// 카운터 피격 모션
    /// </summary>
    public void IsCountered(bool type = true)
    {
        animator.SetBool(CounteredHash, type);
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
        if (pattern.AnimHash == 0) return;
        activeSkillHash = pattern.AnimHash;
        animator.SetBool(activeSkillHash, true);
    }


    /// <summary>
    /// 스킬 종료 제어
    /// </summary>
    /// <param name="skillName"></param>
    /// <param name="value"></param>
    public void SetSkill(int skillNum, bool value)
    {
        int hash = GetSkillHash(skillNum);
        if (hash != 0) animator.SetBool(hash, value);
    }

    /// <summary>
    /// 진행 중인 스킬 중단 (카운터 등)
    /// </summary>
    public void StopActiveSkill()
    {
        if (activeSkillHash == 0) return;
        animator.SetBool(activeSkillHash, false);
        activeSkillHash = 0;
    }

    /// 공격·스킬·이동 Bool을 전부 끔 (사망 시 다른 상태로 끌려가지 않게)
    /// </summary>
    public void ResetActions()
    {
        StopActiveSkill();
        for (int i = 1; i <= MAX_SKILL_SLOT; i++)
            SetSkill(i, false);

        IsNormalAttack(false);
        IsCountered(false);
        IsRun(false);
        IsWalk(false);

        // 연출·스킬 도중 바뀐 재생 속도 복구 (중간에 끊긴 히트스톱 등)
        animator.speed = 1f;
        SetMoveAnimationSpeed(1f);
    }
}
