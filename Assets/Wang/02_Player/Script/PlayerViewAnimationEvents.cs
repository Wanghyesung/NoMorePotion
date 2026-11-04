using UnityEngine;

/*///////////////////////////////////////////
             PlayerViewAnimationEvents
기능 : PlayerView의 애니메이션 이벤트를 Player의 FSM으로 전달
/*///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class PlayerViewAnimationEvents : MonoBehaviour
{
    [SerializeField] private FSM owner;

    public void ExecuteEvent(AnimationEvent animationEvent)
    {
        owner.ExecuteEvent(animationEvent);
    }
}
