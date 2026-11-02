using UnityEngine;

[CreateAssetMenu(fileName = "Idle", menuName = "Player/State/Idle")]
public sealed class SOIdleState : SOState
{
    public override FSM.StateType State => FSM.StateType.Idle;
    public override PlayerState CreateState(FSM owner) => new PlayerIdleState(owner, this);
}
