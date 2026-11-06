using UnityEngine;

using static JJH_Constants;

using SF = UnityEngine.SerializeField;


public class WolfAnamationController : MonsterAnimationController
{
    private static readonly int PoundHash = Animator.StringToHash(POUND);

    [SF] private float baseSpeed = 1;
    public void IsPound(bool type = true)
    {
        animator.SetBool(PoundHash, type);
    }

    /// <summary>
    /// 애니메이션 클립 속도 조절 함수
    /// <para>ex) 0 = 정지, 0.3 = 0.3배속</para>
    /// </summary>
    /// <param name="scale"></param>
    public void SetPlaybackScale(float scale)
    {
        animator.speed = baseSpeed * scale;
    }

    /// <summary>
    /// 애니메이션 클립 속도 초기화 함수
    /// </summary>
    public void ResetPlayback()
    {
        animator.speed = baseSpeed;
    }

}
