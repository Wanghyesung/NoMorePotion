using UnityEngine;

public abstract class PlayerState
{
    protected readonly FSM owner;
    protected readonly SOState data;

    public FSM.StateType Type => data.State;
    public virtual Vector2 MoveDirection => Vector2.zero;

    protected PlayerState(FSM owner, SOState data)
    {
        this.owner = owner;
        this.data = data;
    }

    public virtual void Init() { }
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
