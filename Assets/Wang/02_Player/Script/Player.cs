using System.Collections.Generic;
using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using UnityEngine.InputSystem;
using static InputManager;

/*///////////////////////////////////////////
                      Player
기능 : 플레이어 생명주기와 HP를 관리하고 받은 피해, 외부 자극을 FSM에 전달
 *///////////////////////////////////////////

public class PlayerCharacter : Character
{
    public PlayerCharacter(string name, int maxHp, int hp, Dictionary<DamageType, int> immune)
        : base(name, maxHp, hp, immune) { }
}


[DisallowMultipleComponent]
public sealed class Player : MonoBehaviour, IDamageable, IHealable
{
    private FSM fsm;
    private BuffContainer buffContainer;

    // 마시기로 판정할 실제 누름 시간(초). Inspector 값을 사용하며 게임 시간 배율의 영향을 받지 않는다.
    [SerializeField, Min(0.01f)] private float drinkHoldSeconds = 0.3f;
    private Hand hand;
    private float pressedAt;
    private bool pending;



    private Character character = null;
    public Character Character => character;
    public bool ConfusePotionUse { get; set; }

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        // 일반 회복으로 사망 상태를 부활시키지 않는다.
        if (character.HP <= 0)
            return;

        character.HP += Mathf.Min(amount, Mathf.Max(0, character.MaxHP - character.HP));
    }

    public void TakeDamage(CommonStruct.AttackInfo attackInfo)
    {
        if (character.HP <= 0)
            return;

        float defenseMultiplier = 1f;
        if (buffContainer != null)
            defenseMultiplier = buffContainer.GetStatMultiplier(StatType.Defense);

        // 피해 / 방어력 배율: +20%(1.2배)이면 피해 100 → 83. 0.01은 0 나눗셈 방지용 하한이다.
        int damage = Mathf.Max(0, Mathf.RoundToInt(attackInfo.Damage / Mathf.Max(0.01f, defenseMultiplier)));
        character.HP = Mathf.Max(0, character.HP - damage);
        // HP 차감량만 보정한다. 피격 위치·넉백에 쓰는 원본 공격 정보는 유지한다.
        fsm.TakeDamage(attackInfo);
    }

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, IAttacker attacker)
        => TakeDamage(attackInfo);

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, Character attacker)
        => TakeDamage(attackInfo);

    private void Awake()
    {
        Dictionary<DamageType, int> immune = new();
        // 현재 플레이어의 기본 최대 HP / 시작 HP. 버프 컨테이너는 HP 자체를 보관하지 않는다.
        character = new PlayerCharacter("Player", 100, 100, immune);

        fsm = GetComponent<FSM>();
        fsm.Init(this);
        hand = GetComponent<Hand>();
    }

   

    private void Start()
    {
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

        SubscribePotionInput();
    }

    private void SubscribePotionInput()
    {
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
        if (InputManager.Instance.Skill0Action != null)
            SubscribePotionInput();
        if (fsm.isActiveAndEnabled == true)
            fsm.StartState();


    }

    private void Update()
    {
        UpdatePotionInput();
        if (Character.HP > 0)
            fsm.UpdateState();
    }

    private void FixedUpdate()
    {
        if (Character.HP > 0)
            fsm.FixedUpdateState();
    }

    private void OnDisable()
    {
        CancelPending();
        if (InputManager.Instance != null)
        {
            InputManager.Instance.Skill0Action.started -= OnPressed;
            InputManager.Instance.Skill0Action.canceled -= OnReleased;
            fsm.OnHitAction -= OnHit;
        }
        fsm.Stop();
    }


    private bool CheckState()
    {
        return isActiveAndEnabled == true && Character.HP > 0 && fsm.IsFrozen == false;
    }

    private bool CanStartUse()
    {
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
        if (ConfusePotionUse == true && Random.Range(0, 100) < 50)
            return requestedType == FSM.StateType.Throw ? FSM.StateType.Drink : FSM.StateType.Throw;

        return requestedType;
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
