using System;
using System.Collections.Generic;
using UnityEngine;
using static InputManager;

[DisallowMultipleComponent]
public sealed class FSM : MonoBehaviour
{
    public enum StateType
    {
        Idle = 0,
        Walk = 1,

        Jump = 2,
        Roll = 3,

        Throw = 4,
    }
    public bool HasGameplayInput => Instance.HasGameplayInput;

    #region Components
    private Player player;
    private int upperBodyLayer;
    private static readonly int upperBodyEmptyHash = Animator.StringToHash("UpperBody.Empty");
    public Animator Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    #endregion

    #region PlayerState
    [SerializeField] private List<SOState> stateData = new List<SOState>();
    [SerializeField] private StateType initialState = StateType.Idle;

    private readonly Dictionary<StateType, PlayerState> states = new Dictionary<StateType, PlayerState>(3);
    private readonly Dictionary<StateType, AnimatorEventBridge> animEvents = new();

    public PlayerState CurrentState { get; private set; }
    public PlayerState CurrentActionState { get; private set; }
    public PlayerState PreviousState { get; private set; }
    #endregion

    #region Rotate
    [SerializeField, Min(0f)] private float rotateSensitivity = 0.15f;
    private float rotateY;
    public Quaternion ViewRotation => Quaternion.Euler(0f, rotateY, 0f);
    #endregion

    #region GroundCheck
    private readonly HashSet<Collider> groundContacts = new HashSet<Collider>();
    [SerializeField] private LayerMask groundLayers = ~0;
    public bool IsGrounded => groundContacts.Count > 0 && Rigidbody.linearVelocity.y <= 0.1f;
    public void ClearGroundContact() => groundContacts.Clear();
    #endregion


    public void Init(Player player)
    {
        this.player = player;
        Animator = GetComponent<Animator>();
        upperBodyLayer = Animator.GetLayerIndex("UpperBody");
        Rigidbody = GetComponent<Rigidbody>();

        rotateY = transform.eulerAngles.y;

        foreach (SOState data in stateData)
        {
            PlayerState state = data.CreateState(this);
            states.Add(data.State, state);
            animEvents.Add(data.State, state.AnimEvent);
        }

        foreach (PlayerState state in states.Values)
            state.Init();
    }

    public bool StartState()
    {
        return HasGameplayInput && ChangeState(initialState);
    }

    public void UpdateState()
    {  
        if (CurrentState == null)
        {
            if (!StartState())
                return;
        }

        if (Instance.HasGameplayInput == true && CurrentState.Type != StateType.Roll)
        {
            rotateY = Mathf.Repeat(
                rotateY + Instance.InputInfo.Delta.x * rotateSensitivity, 360f);

            Rigidbody.MoveRotation(ViewRotation);
        }

        CurrentState?.UpdateState();
        CurrentActionState?.UpdateState();
    }

    public void FixedUpdateState()
    {
        CurrentState?.FixedUpdateState();
        CurrentActionState?.FixedUpdateState();
    }

    public void ExecuteEvent(AnimationEvent animationEvent)
    {
        if (CurrentState != null)
            animEvents[CurrentState.Type].ExecuteFrame(animationEvent);
        if (CurrentActionState != null)
            animEvents[CurrentActionState.Type].ExecuteFrame(animationEvent);
    }

    public bool ChangeState(StateType type)
    {
        PlayerState nextState = states[type];
        if (CurrentState == nextState || !nextState.CanEnter())
            return false;

        PreviousState = CurrentState;
        PreviousState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
        return true;
    }

    public bool ChangeActionState(StateType type)
    {
        PlayerState nextState = states[type];
        if (CurrentState == null || CurrentActionState == nextState || !nextState.CanEnter())
            return false;

        ClearActionState();
        CurrentActionState = nextState;
        Animator.SetLayerWeight(upperBodyLayer, 1f);
        CurrentActionState.Enter();
        return true;
    }

    public void ClearActionState()
    {
        CurrentActionState?.Exit();
        CurrentActionState = null;

        Animator.SetLayerWeight(upperBodyLayer, 0f);
        Animator.Play(upperBodyEmptyHash, upperBodyLayer, 0f);
    }

    public void Stop()
    {
        ClearActionState();
        if (CurrentState != null)
        {
            PreviousState = CurrentState;
            CurrentState.Exit();
            CurrentState = null;
        }

        StopHorizontalMovement();
        Animator.speed = 1f;
    }

    public void StopHorizontalMovement()
    {
        Vector3 velocity = Rigidbody.linearVelocity;
        velocity.x = 0f;
        velocity.z = 0f;
        Rigidbody.linearVelocity = velocity;
    }

    public void Dispose()
    {
        foreach (PlayerState state in states.Values)
            state.Dispose();

        states.Clear();
        animEvents.Clear();
        groundContacts.Clear();
        PreviousState = null;
    }

    private void OnEnable()
    {
        if (player.isActiveAndEnabled == true )
            StartState();
    }

    private void OnDisable()
    {
        Stop();
        groundContacts.Clear();
    }

    private void OnDestroy()
    {
        Dispose();
    }

    private void OnCollisionEnter(Collision collision) => UpdateGroundContact(collision);
    private void OnCollisionStay(Collision collision) => UpdateGroundContact(collision);
    private void OnCollisionExit(Collision collision) => groundContacts.Remove(collision.collider);

    private void UpdateGroundContact(Collision collision)
    {
        if ((groundLayers.value & (1 << collision.gameObject.layer)) != 0)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y >= 0.5f)
                {
                    groundContacts.Add(collision.collider);
                    return;
                }
            }
        }

        groundContacts.Remove(collision.collider);
    }
}
