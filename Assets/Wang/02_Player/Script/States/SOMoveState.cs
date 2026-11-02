using UnityEngine;

[CreateAssetMenu(fileName = "Walk", menuName = "Player/State/Walk")]
public sealed class SOMoveState : SOState
{
    [UnityEngine.Serialization.FormerlySerializedAs("m_fMoveSpeed")]
    [UnityEngine.Serialization.FormerlySerializedAs("fMoveSpeed")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [UnityEngine.Serialization.FormerlySerializedAs("m_fDirectionTurnSpeed")]
    [UnityEngine.Serialization.FormerlySerializedAs("fDirectionTurnSpeed")]
    [SerializeField, Min(1f)] private float directionTurnSpeed = 360f;

    public override FSM.StateType State => FSM.StateType.Walk;

    public override PlayerState CreateState(FSM owner)
        => new PlayerMoveState(owner, this, moveSpeed, directionTurnSpeed);
}
