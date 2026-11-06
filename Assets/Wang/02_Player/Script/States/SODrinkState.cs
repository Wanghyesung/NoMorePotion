using UnityEngine;

[CreateAssetMenu(fileName = "Drink", menuName = "Player/State/Drink")]
public sealed class SODrinkState : SOState
{
    [SerializeField] private AnimationClip rightClip;
    public AnimationClip RightClip => rightClip;

    public override FSM.StateType State => FSM.StateType.Drink;
    public override PlayerState CreateState(FSM owner) => new PlayerDrinkState(owner, this);
}
