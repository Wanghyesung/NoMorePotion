using UnityEngine;

[CreateAssetMenu(menuName = "Player/State/Dead")]
public sealed class SODeadState : SOState
{
    public override FSM.StateType State => FSM.StateType.Dead;
    public override PlayerState CreateState(FSM owner) => new PlayerDeadState(owner, this);
}
