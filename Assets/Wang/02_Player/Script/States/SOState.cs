using UnityEngine;

public abstract class SOState : ScriptableObject
{
    [SerializeField] private AnimationClip clip;
    [SerializeField, Min(0.01f)] private float animationSpeed = 1f;

    public AnimationClip Clip => clip;
    public float AnimationSpeed => animationSpeed;
    public abstract FSM.StateType State { get; }
    public abstract PlayerState CreateState(FSM owner);
}
