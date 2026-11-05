using System;
using System.Collections.Generic;
using UnityEngine;
using static InputManager;

/*///////////////////////////////////////////
                       FSM
기능 : 기본·상체 상태 전환과 애니메이션 이벤트를 관리
       피격 전달 및 상태·회전 잠금 처리
 *///////////////////////////////////////////

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
        Pick = 5,
        Hit = 6,
        Take = 7,
        Dead = 8,
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
    public event Action<CommonStruct.AttackInfo> OnHitAction;
    #endregion

    #region Rotate
    [SerializeField] private Transform playerView;
    public Transform PlayerView => playerView;
    [SerializeField, Min(0f)] private float rotateSensitivity = 0.15f;
    private float rotateY;
    // 하위 16비트: 기본 상태, 상위 16비트: 액션 상태
    private int rotateLockMask;
    private bool stateLocked;
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
        Animator = playerView.GetComponent<Animator>();
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

        
        StartState();
    }

    public bool StartState()
    {
        return HasGameplayInput && ChangeState(initialState);
    }

    public void UpdateState()
    {  
        if (CurrentState == null)
        {
            if (StartState() == false)
                return;
        }

        if (Instance.HasGameplayInput == true && rotateLockMask == 0)
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

    // source: 현재 기본/액션 상태. StateType(0~15)에 해당하는 비트만 변경
    public void LockRotate(PlayerState source, bool locked)
    {
        int stateIndex = (int)source.Type;
        int shift = source == CurrentActionState ? 16 : 0;
        int mask = 1 << (shift + stateIndex);

        if (locked == true)
            rotateLockMask |= mask;
        else
            rotateLockMask &= ~mask;
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
        if (stateLocked == true || player.Character.HP <= 0)
            return false;

        PlayerState nextState = states[type];
        if (CurrentState == nextState || nextState.CanEnter() == false)
            return false;

        EnterState(nextState);
        return true;
    }

    public void LockState(bool locked) => stateLocked = locked;

    // attackInfo: 피격 진입 전에 전달할 공격 위치와 피해량
    public void TakeDamage(CommonStruct.AttackInfo attackInfo)
    {
        if (player.Character.HP <= 0)
        {
            PlayerDeadState dead = (PlayerDeadState)states[StateType.Dead];
            if (CurrentState == dead)
                return;

            dead.SetAttackInfo(attackInfo);
            ClearActionState();
            EnterState(dead);
            return;
        }

        OnHitAction?.Invoke(attackInfo);
    }

    // 잠긴 상태나 같은 상태도 중단 후 다시 진입
    public void ResetState(PlayerState nextState)
    {
        if (player.Character.HP <= 0)
            return;

        ClearActionState();
        EnterState(nextState);
    }

    private void EnterState(PlayerState nextState)
    {
        PreviousState = CurrentState;
        PreviousState?.Exit();
        CurrentState = nextState;
        CurrentState.Enter();
    }

    public bool ChangeActionState(StateType type)
    {
        if (stateLocked == true || player.Character.HP <= 0)
            return false;

        PlayerState nextState = states[type];
        if (CurrentState == null || CurrentActionState == nextState || nextState.CanEnter() == false)
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
        rotateLockMask = 0;
        stateLocked = false;
        Animator.speed = 1f;
    }

    public void StopHorizontalMovement()
    {
        if (Rigidbody.isKinematic)
            return;

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
