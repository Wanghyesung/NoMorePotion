using UnityEngine;

/*///////////////////////////////////////////
                    SOShakeState
기능 : 왼손·오른손·양손 흔들기 클립과 재생 속도를 설정
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "Shake", menuName = "Player/State/Shake")]
public sealed class SOShakeState : SOState
{
    [SerializeField] private AnimationClip rightClip;
    [SerializeField] private AnimationClip bothClip;
    public AnimationClip RightClip => rightClip;
    public AnimationClip BothClip => bothClip;

    public override FSM.StateType State => FSM.StateType.Shake;
    public override PlayerState CreateState(FSM owner) => new PlayerShakeState(owner, this);
}
