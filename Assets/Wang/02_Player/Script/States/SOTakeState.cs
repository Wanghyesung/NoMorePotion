using UnityEngine;

/*///////////////////////////////////////////
                    SOTakeState
기능 : 양손 꺼내기 클립과 재생 속도를 설정하고 런타임 상태 생성
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "Take", menuName = "Player/State/Take")]
public sealed class SOTakeState : SOState
{
    [SerializeField] private AnimationClip rightClip;
    public AnimationClip RightClip => rightClip;
    public override FSM.StateType State => FSM.StateType.Take;
    public override PlayerState CreateState(FSM owner) => new PlayerTakeState(owner, this);
}
