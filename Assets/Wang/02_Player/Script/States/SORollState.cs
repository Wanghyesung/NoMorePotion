using UnityEngine;

[CreateAssetMenu(fileName = "Roll", menuName = "Player/State/Roll")]
public sealed class SORollState : SOState
{
    [UnityEngine.Serialization.FormerlySerializedAs("m_fRollPower")]
    [UnityEngine.Serialization.FormerlySerializedAs("fRollPower")]
    [SerializeField, Min(0f)] private float rollPower = 8f;
    [UnityEngine.Serialization.FormerlySerializedAs("m_fRollDeceleration")]
    [UnityEngine.Serialization.FormerlySerializedAs("fRollDeceleration")]
    [SerializeField, Min(0f)] private float rollDeceleration = 16f;

    public override FSM.StateType State => FSM.StateType.Roll;
    public override PlayerState CreateState(FSM owner)
        => new PlayerRollState(owner, this, rollPower, rollDeceleration);
}
