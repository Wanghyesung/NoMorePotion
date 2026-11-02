using UnityEngine;

public abstract class SOState : ScriptableObject
{
    [UnityEngine.Serialization.FormerlySerializedAs("m_refClip")]
    [UnityEngine.Serialization.FormerlySerializedAs("refClip")]
    [SerializeField] private AnimationClip clip;
    [UnityEngine.Serialization.FormerlySerializedAs("m_fAnimationSpeed")]
    [UnityEngine.Serialization.FormerlySerializedAs("fAnimationSpeed")]
    [SerializeField, Min(0.01f)] private float animationSpeed = 1f;

    public AnimationClip Clip => clip;
    public float AnimationSpeed => animationSpeed;
    public abstract FSM.StateType State { get; }
    public abstract PlayerState CreateState(FSM owner);
}
