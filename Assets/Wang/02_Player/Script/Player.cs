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
public sealed class Player : MonoBehaviour, IDamageable, IHealable, IStatusEffectReceiver
{
    private FSM fsm;
    private BuffContainer buffContainer;
    [SerializeField, Min(0.01f)] private float drinkHoldSeconds = 0.3f;
    private Hand hand;
    private float pressedAt;
    private bool pending;



    private Character character = null;
    public Character Character => character;

    public bool ApplyStatusEffect(StatusEffectData data, object source, int percentOverride = 0, float duration = 0f)
    {
        return buffContainer.Apply(data, source, percentOverride, duration);
    }

    public int RemoveStatusEffects(object source)
    {
        if (buffContainer == null)
            return 0;

        return buffContainer.RemoveBySource(source);
    }


    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

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

        int damage = Mathf.Max(0, Mathf.RoundToInt(attackInfo.Damage / Mathf.Max(0.01f, defenseMultiplier)));
        character.HP = Mathf.Max(0, character.HP - damage);
        fsm.TakeDamage(attackInfo);
    }

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, IAttacker attacker)
        => TakeDamage(attackInfo);

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, Character attacker)
        => TakeDamage(attackInfo);

    private void Awake()
    {
        Dictionary<DamageType, int> immune = new();
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
        return isActiveAndEnabled == true && Character.HP > 0;
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
            CancelPending();
            fsm.ChangeActionState(FSM.StateType.Drink);
        }
    }

    private void OnReleased(InputAction.CallbackContext context)
    {
        if (pending == false)
            return;

        bool valid = CanStartUse();
        bool held = Time.unscaledTime - pressedAt >= drinkHoldSeconds;
        CancelPending();
        if (valid == true)
            fsm.ChangeActionState(held == true ? FSM.StateType.Drink : FSM.StateType.Throw);
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
