using System.Collections.Generic;
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
public sealed class Player : MonoBehaviour, IDamageable
{
    private FSM fsm;
    [SerializeField, Min(0.01f)] private float drinkHoldSeconds = 1f;
    private Hand hand;
    private InputAction useAction;
    private float pressedAt;
    private bool pending;



    private Character character = null;
    public Character Character => character;


    public void TakeDamage(CommonStruct.AttackInfo attackInfo)
    {
        if (character.HP <= 0)
            return;

        character.HP = Mathf.Max(0, character.HP - attackInfo.Damage);
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
        useAction = InputManager.Instance.Skill0Action;
        SubscribePotionInput();
    }

    private void SubscribePotionInput()
    {
        useAction.started += OnPressed;
        useAction.canceled += OnReleased;
        fsm.OnHitAction += OnHit;
    }

    private void OnEnable()
    {
        if (useAction != null)
            SubscribePotionInput();
        if (fsm.isActiveAndEnabled == true)
            fsm.StartState();


    }

    //나중에 버릴 코드
    //private void Update()
    //{
    //    // 피격 테스트: O를 누르는 동안 전방 2m의 랜덤 위치에서 공격

    //    bool testHit = Input.GetKeyDown(KeyCode.O);
    //    if (Character.HP > 0 && testHit == true)
    //    {
    //        Vector3 attackDirection = Quaternion.AngleAxis(Random.Range(-45f, 45f), Vector3.up)
    //            * transform.forward;
    //        TakeDamage(new CommonStruct.AttackInfo
    //        {
    //            Position = transform.position + attackDirection * 2.0f,
    //            Damage = 10,
    //            AttackPower = 10
    //        });
    //    }
        
    //    if (Character.HP > 0)
    //        fsm.UpdateState();
    //}

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
        if (useAction != null)
        {
            useAction.started -= OnPressed;
            useAction.canceled -= OnReleased;
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
