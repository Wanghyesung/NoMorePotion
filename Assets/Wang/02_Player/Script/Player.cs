using UnityEngine;
using static InputManager;

/*///////////////////////////////////////////
                      Player
기능 : 플레이어 생명주기와 HP를 관리하고 받은 피해를 FSM에 전달
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class Player : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1)] private int hp = 100;
    public int HP => hp;
    private FSM fsm;

    // attackInfo.Position: 공격이 들어온 월드 위치
    public void TakeDamage(CommonStruct.AttackInfo attackInfo)
    {
        if (hp <= 0)
            return;

        hp = Mathf.Max(0, hp - attackInfo.Damage);
        if (hp == 0)
        {
            fsm.Stop();
            fsm.Animator.speed = 0f;
            return;
        }

        fsm.TakeDamage(attackInfo);
    }

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, IAttacker attacker)
        => TakeDamage(attackInfo);

    public void TakeDamage(CommonStruct.AttackInfo attackInfo, Character attacker)
        => TakeDamage(attackInfo);

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
        // 피격 테스트: O를 누르는 동안 전방 2m의 랜덤 위치에서 공격
#if ENABLE_LEGACY_INPUT_MANAGER
        bool testHit = Input.GetKey(KeyCode.O);
#else
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool testHit = keyboard != null && keyboard.oKey.isPressed;
#endif
        if (hp > 0 && testHit)
        {
            Vector3 attackDirection = Quaternion.AngleAxis(Random.Range(-45f, 45f), Vector3.up)
                * transform.forward;
            TakeDamage(new CommonStruct.AttackInfo
            {
                Position = transform.position + attackDirection * 2.0f,
                Damage = 1
            });
        }

        if (hp > 0)
            fsm.UpdateState();
    }

    private void FixedUpdate()
    {
        if (hp > 0)
            fsm.FixedUpdateState();
    }

    private void OnDisable()
    {
        fsm.Stop();
    }


}
