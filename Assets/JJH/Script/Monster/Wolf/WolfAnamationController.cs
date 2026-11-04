using Unity.VisualScripting;
using UnityEngine;

using static JJH_Constants;

public class WolfAnamationController : MonsterAnimationController
{
    private static readonly int PoundHash = Animator.StringToHash(POUND);

    public void IsPound(bool type = true)
    {
        animator.SetBool(PoundHash, type);
    }

}
