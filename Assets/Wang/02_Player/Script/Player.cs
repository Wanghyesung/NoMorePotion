using UnityEngine;
using static InputManager;

[DisallowMultipleComponent]
public sealed class Player : MonoBehaviour
{
 
    private FSM fsm;

    private void Awake()
    {
        fsm = GetComponent<FSM>();
        fsm.Init(this);
    }

    private void OnEnable()
    {
        if (fsm.isActiveAndEnabled)
            fsm.StartState();
    }

    private void Update()
    {
        fsm.UpdateState();
    }

    private void FixedUpdate()
    {
        fsm.FixedUpdateState();
    }

    private void OnDisable()
    {
        fsm.Stop();
    }

    public void OnJumpHold()
    {
        if (fsm.CurrentState is PlayerJumpState jump)
            jump.HoldPose();
    }

    public void OnRollFinished()
    {
        if (fsm.CurrentState is PlayerRollState roll)
            roll.FinishRoll();
    }
}
