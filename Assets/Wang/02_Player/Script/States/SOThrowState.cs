using UnityEngine;

[CreateAssetMenu(fileName = "Throw", menuName = "Player/State/Throw")]
public sealed class SOThrowState : SOState
{
    [SerializeField] private AnimationClip rightClip;
    public AnimationClip RightClip => rightClip;
    public override FSM.StateType State => FSM.StateType.Throw;
    public override PlayerState CreateState(FSM owner) => new PlayerThrowState(owner, this);
}
