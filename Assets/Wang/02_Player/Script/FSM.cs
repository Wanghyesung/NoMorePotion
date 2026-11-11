using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
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
        Drink = 9,
        Shake = 10,
    }
    public bool HasGameplayInput => player != null && player.IsLocalPlayer == true && Instance.HasGameplayInput;
    public bool IsFrozen => freezeRemaining > 0f;
    private float freezeRemaining;
    private float animatorSpeedBeforeFreeze;
    public bool ReverseMovement { get; set; }
    public float MoveSpeedMultiplier { get; set; } = 1f;
    public Vector2 MoveInput
    {
        get
        {
            Vector2 input = Instance.InputInfo.MoveDir;
            // 조작 방향만 반전한다. 외부에서 더하는 넉백 속도에는 영향을 주지 않는다.
            if (ReverseMovement == true)
                input = -input;

            return input;
        }
    }

    #region Components
    private Player player;
    private int upperBodyLayer;
    private static readonly int upperBodyEmptyHash = Animator.StringToHash("UpperBody.Empty");
    public Animator Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    #endregion

    #region Velocity
    public Vector3 Vel { get; private set; }
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
    public bool IsRotationLocked => rotateLockMask != 0;
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

        if (player.IsLocalPlayer == false)
            return;

        foreach (SOState data in stateData)
        {
            PlayerState state = data.CreateState(this);
            states.Add(data.State, state);
            animEvents.Add(data.State, state.AnimEvent);
        }

        foreach (PlayerState state in states.Values)
            state.Init();

        
    }

    public void RefreshState()
    {
        if (player == null || player.IsLocalPlayer == false)
            return;

        Animator.enabled = true;
        Stop();
        PreviousState = null;
        ClearGroundContact();
        Vel = Vector3.zero;
        if (Rigidbody.isKinematic == false)
        {
            Rigidbody.linearVelocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
        }
        rotateY = transform.eulerAngles.y;
        Animator.Rebind();
        Animator.Update(0f);
        Animator.SetLayerWeight(upperBodyLayer, 0f);
        EnterState(states[initialState]);
    }

    public bool StartState()
    {
        return HasGameplayInput && ChangeState(initialState);
    }

    public void UpdateState()
    {  
        // 상태 업데이트를 건너뛰어도 해제 타이머는 반드시 진행한다.
        TickFreeze(Time.deltaTime);

        if (IsFrozen == true)
            return;

        if (CurrentState == null)
        {
            if (StartState() == false)
                return;
        }

        if (Instance.HasGameplayInput == true && rotateLockMask == 0)
        {
            rotateY = Mathf.Repeat(rotateY + Instance.InputInfo.Delta.x * rotateSensitivity, 360f);
            Rigidbody.MoveRotation(ViewRotation);
        }

        CurrentState?.UpdateState();
        CurrentActionState?.UpdateState();
    }

    public void FixedUpdateState()
    {
        if (IsFrozen == true)
            return;

        if (Rigidbody.isKinematic)
            return;

        // 기존 수직 속도는 유지하고, 이번 물리 프레임의 이동·넉백 속도를 새로 합산한다.
        Vel = Vector3.up * Rigidbody.linearVelocity.y;

        CurrentState?.FixedUpdateState();
        CurrentActionState?.FixedUpdateState();

        Rigidbody.linearVelocity = Vel;
    }

    // FixedUpdateState에서 이번 물리 프레임에 적용할 속도를 누적한다.
    public void AddVel(Vector3 velocity) => Vel += velocity;

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
        if (player == null || player.IsLocalPlayer == false)
            return false;
        if (IsFrozen == true)
            return false;

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
        if (player == null || player.IsLocalPlayer == false)
            return;
        if (player.Character.HP <= 0)
        {
            Unfreeze();
            PlayerDeadState dead = (PlayerDeadState)states[StateType.Dead];
            if (CurrentState == dead)
                return;

            dead.SetAttackInfo(attackInfo);
            ClearActionState();
            Vel = Vector3.zero;
            EnterState(dead);

            InventoryNetworkSyncManager.Instance.DieAsync(Rigidbody.position).Forget();
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
        if (player == null || player.IsLocalPlayer == false)
            return false;
        if (IsFrozen == true)
            return false;

        if (stateLocked == true || player.Character.HP <= 0)
            return false;

        if (CurrentActionState != null && CurrentActionState.Type == StateType.Hit)
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

    public void ResetActionState(PlayerState nextState)
    {
        if (player.Character.HP <= 0)
            return;

        ClearActionState();
        CurrentActionState = nextState;
        Animator.SetLayerWeight(upperBodyLayer, 1f);
        CurrentActionState.Enter();
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
        if (player == null || player.IsLocalPlayer == false)
            return;
        Unfreeze();
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

    // 현재 상태는 유지한다. 중복 호출은 남은 시간을 연장하며 원래 재생 속도를 덮어쓰지 않는다.
    public void Freeze(float duration)
    {
        if (player == null || player.IsLocalPlayer == false)
            return;
        if (duration <= 0f || player.Character.HP <= 0)
            return;

        if (IsFrozen == false)
            animatorSpeedBeforeFreeze = Animator.speed;

        freezeRemaining = Mathf.Max(freezeRemaining, duration);
        Animator.speed = 0f;
        // FixedUpdate를 건너뛰어도 기존 속도로 미끄러지지 않도록 진입할 때만 멈춘다.
        StopHorizontalMovement();
    }

    private void TickFreeze(float deltaTime)
    {
        if (IsFrozen == false)
            return;

        if (deltaTime >= freezeRemaining)
            Unfreeze();
        else
            freezeRemaining -= deltaTime;
    }

    public void Unfreeze()
    {
        if (IsFrozen == false)
            return;

        freezeRemaining = 0f;
        Animator.speed = animatorSpeedBeforeFreeze;
    }

    public void StopHorizontalMovement()
    {
        Vel = Vector3.up * Rigidbody.linearVelocity.y;
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
