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
    public Animator Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    #endregion

    #region PlayerState
    [UnityEngine.Serialization.FormerlySerializedAs("m_listState")]
    [UnityEngine.Serialization.FormerlySerializedAs("listState")]
    [SerializeField] private List<SOState> stateData = new List<SOState>();
    [UnityEngine.Serialization.FormerlySerializedAs("m_eInitialState")]
    [UnityEngine.Serialization.FormerlySerializedAs("eInitialState")]
    [SerializeField] private StateType initialState = StateType.Idle;

    private readonly Dictionary<StateType, PlayerState> states = new Dictionary<StateType, PlayerState>(3);

    public PlayerState CurrentState { get; private set; }
    public PlayerState PreviousState { get; private set; }
    #endregion

    #region Rotate
    [UnityEngine.Serialization.FormerlySerializedAs("m_fRotateSensitivity")]
    [UnityEngine.Serialization.FormerlySerializedAs("fRotateSensitivity")]
    [SerializeField, Min(0f)] private float rotateSensitivity = 0.15f;
    private float rotateY;
    public Quaternion ViewRotation => Quaternion.Euler(0f, rotateY, 0f);
    #endregion

    #region GroundCheck
    private readonly HashSet<Collider> groundContacts = new HashSet<Collider>();
    [UnityEngine.Serialization.FormerlySerializedAs("m_iGroundLayers")]
    [UnityEngine.Serialization.FormerlySerializedAs("iGroundLayers")]
    [SerializeField] private LayerMask groundLayers = ~0;
    public bool IsGrounded => groundContacts.Count > 0 && Rigidbody.linearVelocity.y <= 0.1f;
    public void ClearGroundContact() => groundContacts.Clear();
    #endregion


    public void Init(Player player)
    {
        this.player = player;
        Animator = GetComponent<Animator>();
        Rigidbody = GetComponent<Rigidbody>();

        rotateY = transform.eulerAngles.y;

        foreach (SOState data in stateData)
        {
            PlayerState state = data.CreateState(this);
            states.Add(data.State, state);
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
            StartState();

        if (Instance.HasGameplayInput == true && CurrentState.Type != StateType.Roll)
        {
            rotateY = Mathf.Repeat(
                rotateY + Instance.InputInfo.Delta.x * rotateSensitivity, 360f);

            Rigidbody.MoveRotation(ViewRotation);
        }

        CurrentState?.UpdateState();
    }

    public void FixedUpdateState()
    {
        CurrentState?.FixedUpdateState();
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

    public void Stop()
    {
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

    private void OnCollisionEnter(Collision collision) => updateGroundContact(collision);
    private void OnCollisionStay(Collision collision) => updateGroundContact(collision);
    private void OnCollisionExit(Collision collision) => groundContacts.Remove(collision.collider);

    private void updateGroundContact(Collision collision)
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
