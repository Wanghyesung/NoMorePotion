using System.Collections.Generic;
using Photon.Pun;
using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;
using System;
using DamageNumbersPro;

/*///////////////////////////////////////////
                      Player
기능 : 플레이어 생명주기와 HP를 관리하고 받은 피해, 외부 자극을 FSM에 전달
 *///////////////////////////////////////////

public class PlayerCharacter : Character
{
    public int BaseAttackPower { get; }
    public float AttackMultiplier = 1f;
    public float AttackPower => BaseAttackPower * AttackMultiplier;
    public float Defend = 1f; // 방어 배율: 받는 피해를 이 값으로 나눕니다.
    public int maxAttackPower = 10;
    public PlayerCharacter(string name, int maxHp, int hp, Dictionary<DamageType, int> immune , int attackPower, int maxAttackPower)
        : base(name, maxHp, hp, immune)
    {
        BaseAttackPower = attackPower;
        this.maxAttackPower = maxAttackPower;  
    }
}


[DisallowMultipleComponent]
public sealed class Player : MonoBehaviour, IDamageable, IHealable
{
    #region 컴포넌트 참조
    private FSM fsm;
    private PhotonView photonView;
    private BuffContainer buffContainer;
    private Hand hand;
    private AttackTarget attackTarget;
    private PlayerRagdoll ragdoll;
    #endregion

    #region 입력 상태
    private bool started;
    private bool inputSubscribed;
    // 마시기로 판정할 실제 누름 시간(초). Inspector 값을 사용하며 게임 시간 배율의 영향을 받지 않는다.
    [SerializeField, Min(0.01f)] private float drinkHoldSeconds = 0.3f;
    private float pressedAt;
    private bool pending;
    #endregion

    #region 캐릭터 정보
    private PlayerCharacter character = null;

    [SerializeField] private SkinnedMeshRenderer skinMeshRenderer;
    [SerializeField] private PlayerBlink playerBlink;
    [SerializeField] private DamageNumber damageNumberPrefab;
    [SerializeField] private Vector3 damageNumberOffset = new Vector3(0f, 1.8f, 0f);

    public bool IsLocalPlayer => photonView == null || photonView.IsMine == true;
    public Hand Hand => hand;
    public PlayerCharacter Character => character;
    public bool ConfusePotionUse { get; set; }
    #endregion

    #region 카메라와 시선
    public const float MinLookPitch = -30f;
    public const float MaxLookPitch = 40f;
    [SerializeField] private Transform cameraPosition;
    [SerializeField] private Transform lookPivot;
    [SerializeField, Min(0f)] private float pitchSensitivity = 0.15f;
    private float lookPitch;

    public Transform CameraPosition => cameraPosition;
    public Transform LookPivot => lookPivot;
    #endregion

   
    
    public event Action<Vector3> OnDead;
    public void SetLookPitch(float pitch)
    {
        lookPitch = Mathf.Clamp(pitch, MinLookPitch, MaxLookPitch);
        lookPivot.localRotation = Quaternion.Euler(lookPitch, 0f, 0f);
    }


    public void Heal(int amount)
    {
        if (IsLocalPlayer == false || amount <= 0)
            return;

        // 일반 회복으로 사망 상태를 부활시키지 않는다.
        if (character.HP <= 0)
            return;

        character.HP += Mathf.Min(amount, Mathf.Max(0, character.MaxHP - character.HP));
    }

    public void TakeDamage(CommonStruct.AttackInfo attackInfo)
    {
        if (IsLocalPlayer == false)
            return;
        if (character.HP <= 0)
            return;

        // 피해 / 방어력 배율: +20%(1.2배)이면 피해 100 → 83. 0.01은 0 나눗셈 방지용 하한이다.
        int damage = Mathf.Max(0, Mathf.RoundToInt(attackInfo.Damage / Mathf.Max(0.01f, character.Defend)));
        character.HP = Mathf.Max(0, character.HP - damage);

        // 사망하는 타격도 표시한다. 방어력을 반영한 숫자는 소유자가 한 번만 전송한다.
        if (damage > 0)
        {
            Vector3 popupPosition = transform.position + damageNumberOffset;
            ShowDamageNumber(popupPosition, damage);
            if (PhotonNetwork.InRoom && photonView != null && photonView.ViewID != 0)
                photonView.RPC(nameof(RpcShowDamageNumber), RpcTarget.Others, popupPosition, damage);
        }

        if (character.HP <= 0)
        {
            CancelPending();

            if (attackTarget != null)
                attackTarget.SetTargetable(false);

            OnDead?.Invoke(transform.position);
        }

        // HP 차감량만 보정한다. 피격 위치·넉백에 쓰는 원본 공격 정보는 유지한다.
        fsm.TakeDamage(attackInfo);


        //여기 데미지 들어왔을 떄 어떤 애인지 알아야할듯 무조건 애니메이션이 발동되네
    }

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, IAttacker attacker)
        => TakeDamage(attackInfo);

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, Character attacker)
        => TakeDamage(attackInfo);

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        attackTarget = GetComponent<AttackTarget>();
        ragdoll = GetComponent<PlayerRagdoll>();
        
        SetLookPitch(Mathf.DeltaAngle(0f, lookPivot.localEulerAngles.x));
        Dictionary<DamageType, int> immune = new();
        // 현재 플레이어의 기본 최대 HP / 시작 HP. 버프 컨테이너는 HP 자체를 보관하지 않는다.
        character = new PlayerCharacter("Player", 10000, 10000, immune, 1,10);

        fsm = GetComponent<FSM>();
        fsm.Init(this);
        hand = GetComponent<Hand>();
        if (IsLocalPlayer == false)
            fsm.Rigidbody.isKinematic = true;

    }

   

    private void Start()
    {
        started = true;
        if (IsLocalPlayer == false)
            return;
        // 매니저의 Awake가 끝난 뒤 기존 로컬 플레이어 컨테이너를 연결한다.
        SurviveStatusManager statusManager = SurviveStatusManager.Instance;
        if (statusManager != null)
        {
            buffContainer = statusManager.PlayerBuffContainer;
            buffContainer.SetOwner(gameObject);
            statusManager.BindPlayer(gameObject);
        }
        else
        {
            Debug.LogWarning("SurviveStatusManager가 없어 Player의 BuffContainer를 연결하지 못했습니다.", this);
        }

        RefreshPlayer();
        SubscribePotionInput();
        ChangeColor();
    }

    /// <summary>
    /// 생성·부활 시 HP, 상태 효과, 입력 대기, 래그돌과 FSM을 초기화하고 공격 대상으로 복귀합니다.
    /// 부활 위치는 호출 전에 지정합니다. 인벤토리와 손의 포션은 변경하지 않습니다.
    /// </summary>
    public void RefreshPlayer()
    {
        if (IsLocalPlayer == false)
            return;

        bool wasDead = character.HP <= 0;
        CancelPending();

        if (buffContainer != null)
        {
            buffContainer.Clear();

            // 포션 등이 건 효과만 지우고, 생존 상태 구간이 거는 효과(예: 정신력 초기값 50 의 멍청함)는 현재 구간에 맞게 다시 건다.
            if (SurviveStatusManager.Instance != null)
                SurviveStatusManager.Instance.ResetAllStatus();
        }

        character.HP = character.MaxHP;
        ConfusePotionUse = false;
        fsm.ReverseMovement = false;
        fsm.MoveSpeedMultiplier = 1f;

        if (ragdoll != null)
            ragdoll.Restore();

        SetLookPitch(0f);
        fsm.RefreshState();
        if (attackTarget != null)
            attackTarget.SetTargetable(true);

        // 최초 생성 시 초기화는 제외하고, 실제 부활만 다른 클라이언트에 전달한다.
        if (wasDead == true && PhotonNetwork.InRoom && photonView != null && photonView.ViewID != 0)
            photonView.RPC(nameof(RpcRevive), RpcTarget.Others, transform.position, transform.rotation);
    }

    [PunRPC]
    private void RpcRevive(Vector3 position, Quaternion rotation, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner || photonView.IsMine == true)
            return;

        // 원격에서는 로컬 전용 HP/버프/FSM 초기화 대신 캐릭터의 표현만 복구한다.
        if (ragdoll != null)
            ragdoll.Restore();

        transform.SetPositionAndRotation(position, rotation);
        fsm.Rigidbody.position = position;
        fsm.Rigidbody.rotation = rotation;
        SetLookPitch(0f);

        Animator animator = fsm.Animator;
        animator.enabled = true;
        animator.speed = 1f;
        animator.Rebind();
        animator.SetLayerWeight(animator.GetLayerIndex("UpperBody"), 0f);
        animator.Update(0f);

        if (attackTarget != null)
            attackTarget.SetTargetable(true);
    }

    private void SubscribePotionInput()
    {
        if (IsLocalPlayer == false || inputSubscribed == true)
            return;
        inputSubscribed = true;
        InputManager.Instance.Skill0Action.started += OnPressed;
        InputManager.Instance.Skill0Action.canceled += OnReleased;
        fsm.OnHitAction += OnHit;
    }

    private void OnDestroy()
    {
        if (buffContainer != null && buffContainer.Owner == gameObject)
            buffContainer.SetOwner(null);
    }

    private void OnEnable()
    {
        if (IsLocalPlayer == false || started == false)
            return;
        SubscribePotionInput();
        if (fsm.isActiveAndEnabled == true)
            fsm.StartState();


    }

    private void Update()
    {
        if (IsLocalPlayer == false)
            return;
        UpdatePotionInput();
        if (Character.HP > 0)
        {
            if (Instance.HasGameplayInput == true && fsm.IsFrozen == false && fsm.IsRotationLocked == false)
                SetLookPitch(lookPitch - Instance.InputInfo.Delta.y * pitchSensitivity);
            fsm.UpdateState();
        }

        if(Keyboard.current[Key.F8].wasPressedThisFrame)
        {
            RefreshPlayer();
        }
    }

    private void FixedUpdate()
    {
        if (IsLocalPlayer == false)
            return;
        if (Character.HP > 0)
            fsm.FixedUpdateState();
    }

    private void OnDisable()
    {
        CancelPending();
        if (inputSubscribed == true && InputManager.Instance != null)
        {
            InputManager.Instance.Skill0Action.started -= OnPressed;
            InputManager.Instance.Skill0Action.canceled -= OnReleased;
            fsm.OnHitAction -= OnHit;
        }
        inputSubscribed = false;
        if (IsLocalPlayer == true)
            fsm.Stop();
    }


    private bool CheckState()
    {
        return isActiveAndEnabled == true && Character.HP > 0 && fsm.IsFrozen == false;
    }

    private bool CanStartUse()
    {
        // 액션 잠금도 canceled 콜백을 발생시키므로 키를 뗀 것으로 처리하지 않습니다.
        if (!Instance.HasGameplayInput || (Instance.GuideAllowedKeys & GameplayKeys.UsePotion) == 0)
            return false;

        if (CheckState() == false)
            return false;

        if (fsm.CurrentActionState != null || fsm.CurrentState == null)
            return false;

        if (fsm.CurrentState.Type != FSM.StateType.Idle && fsm.CurrentState.Type != FSM.StateType.Walk)
            return false;

        if (hand.FocusedPotion == null)
            return false;

        return true;
    }

    private void OnPressed(InputAction.CallbackContext context)
    {
        CancelPending();
        if (CheckState() == false)
            return;

        if (fsm.CurrentActionState is PlayerThrowState throwing && throwing.IsAiming == true)
        {
            throwing.RequestThrow();
            return;
        }

        if (CanStartUse() == false)
            return;

        pressedAt = Time.unscaledTime;
        pending = true;
    }

    private void UpdatePotionInput()
    {
        if (pending == false)
            return;

        if (CanStartUse() == false)
        {
            CancelPending();
            return;
        }

        if (Time.unscaledTime - pressedAt >= drinkHoldSeconds)
        {
            // 먼저 대기를 해제해야 버튼을 뗄 때 중복 사용·확률 재판정이 발생하지 않는다.
            CancelPending();
            RequestPotionUse(FSM.StateType.Drink);
        }
    }

    private void OnReleased(InputAction.CallbackContext context)
    {
        if (pending == false)
            return;

        // 이번 프레임의 Update보다 해제 콜백이 먼저 왔어도 누른 시간으로 사용 의도를 결정한다.
        bool held = Time.unscaledTime - pressedAt >= drinkHoldSeconds;
        CancelPending();
        RequestPotionUse(held == true ? FSM.StateType.Drink : FSM.StateType.Throw);
    }

    private void RequestPotionUse(FSM.StateType requestedType)
    {
        if (CanStartUse() == false)
            return;

        fsm.ChangeActionState(ResolvePotionUse(requestedType));
    }

    private FSM.StateType ResolvePotionUse(FSM.StateType requestedType)
    {
        // 사용 의도가 확정될 때 한 번만 판정한다. 조준 중 발사 확정은 이 경로를 거치지 않는다.
        // 테스트용 50%: 정수 0~99 중 0~49에서 교체한다. 기획 확률은 5%다.
        if (ConfusePotionUse == true && UnityEngine.Random.Range(0, 100) < 5)
            return requestedType == FSM.StateType.Throw ? FSM.StateType.Drink : FSM.StateType.Throw;

        return requestedType;
    }

    public void ChangeColor()
    {
        if (IsLocalPlayer == false)
            return;

        int index = PlayerProps.GetColorIndex(photonView.Owner);
        ApplyColor(index);
        if (PhotonNetwork.InRoom && photonView != null && photonView.ViewID != 0)
            photonView.RPC(nameof(RpcChangeColor), RpcTarget.Others , index);
    }

    [PunRPC]
    private void RpcChangeColor(int index, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Owner || photonView.IsMine == true)
            return;

        ApplyColor(index);
    }

    private void ApplyColor(int index)
    {
        // 임시로 4번 색상 고정. 첫 번째 머티리얼인 몸 색상만 변경한다.
        
        Color color = PlayerColors.Get(index);
        skinMeshRenderer.material.color = color;
        playerBlink.SetBaseColor(color);
    }


    [PunRPC]
    private void RpcShowDamageNumber(Vector3 position, int damage, PhotonMessageInfo info)
    {
        // 이 플레이어 소유자가 확정한 데미지만 표시한다. 원격 HP는 변경하지 않는다.
        if (info.Sender != photonView.Owner || photonView.IsMine == true)
            return;

        ShowDamageNumber(position, damage);
    }

    private void ShowDamageNumber(Vector3 position, int damage)
    {
        // 숫자 연출을 연결하지 않은 테스트 플레이어도 피격 처리는 유지한다.
        if (damageNumberPrefab == null || damage <= 0)
            return;

        damageNumberPrefab.Spawn(position, damage);
    }

    private void OnHit(CommonStruct.AttackInfo info) => CancelPending();

    private void OnApplicationFocus(bool focused)
    {
        if (focused == false)
            CancelPending();
    }

    private void CancelPending()
    {
        pending = false;
    }
}
