using UnityEngine;

public abstract class PlayerState
{
    protected readonly FSM owner;
    protected readonly SOState data;
    protected readonly AnimatorEventBridge animEvent = new AnimatorEventBridge();
    protected readonly AnimationClip clip;
    protected AnimationEvent[] clipEvents;

    public AnimatorEventBridge AnimEvent => animEvent;

    public FSM.StateType Type => data.State;
    public virtual Vector2 MoveDirection => Vector2.zero;

    protected PlayerState(FSM owner, SOState data)
    {
        this.owner = owner;
        this.data = data;
        clip = data.Clip;
    }

    public virtual void Init() => clipEvents = clip.events;
    public virtual void Dispose() { }
    public virtual bool CanEnter() => true;

    public virtual void Enter()
    {
        owner.Animator.speed = data.AnimationSpeed;
    }

    public virtual void UpdateState() { }
    public virtual void FixedUpdateState() { }
    public virtual void Exit()
    {
        owner.Animator.speed = 1f;
    }
}
