using UnityEngine;

[CreateAssetMenu(fileName = "Jump", menuName = "Player/State/Jump")]
public sealed class SOJumpState : SOState
{
    [SerializeField, Min(0.1f)] private float jumpSpeed = 6f;

    public override FSM.StateType State => FSM.StateType.Jump;
    public override PlayerState CreateState(FSM owner)
        => new PlayerJumpState(owner, this, jumpSpeed);
}
