using UnityEngine;

/*///////////////////////////////////////////
                  SOPickState
기능 : 줍기 클립과 재생 속도를 설정하고 런타임 상태를 생성
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "Pick", menuName = "Player/State/Pick")]
public sealed class SOPickState : SOState
{
    public override FSM.StateType State => FSM.StateType.Pick;
    public override PlayerState CreateState(FSM owner) => new PlayerPickState(owner, this);
}
