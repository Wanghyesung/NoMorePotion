using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using static InputManager;

/*///////////////////////////////////////////
                      Player
기능 : 플레이어 생명주기와 HP를 관리하고 받은 피해를 FSM에 전달
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
    private MMF_Player viewFeedbacks;
    private bool testingViewFeedbacks;

    private Character character = null;
    public Character Character => character;
    // attackInfo.Position: 공격이 들어온 월드 위치


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
        viewFeedbacks = GetComponentInChildren<MMF_Player>();


        //Assets/Imports/Feel/FeelDemos/Toaster/FeelToaster.unity

    }

    private void OnEnable()
    {
        if (fsm.isActiveAndEnabled == true)
            fsm.StartState();
    }

    //나중에 버릴 코드
    private void Update()
    {
        // 피격 테스트: O를 누르는 동안 전방 2m의 랜덤 위치에서 공격
        bool testHit = Input.GetKey(KeyCode.O);
        bool testView = Input.GetKey(KeyCode.P);
        // P를 누르는 동안 한 번의 재생이 끝날 때마다 반복한다.
        if (testView == true && viewFeedbacks != null)
        {
            testingViewFeedbacks = true;
            if (viewFeedbacks.IsPlaying == false)
                viewFeedbacks.PlayFeedbacks();
        }
        else
        {
            StopViewFeedbackTest();
        }
        
        if (Character.HP > 0 && testHit == true)
        {
            Vector3 attackDirection = Quaternion.AngleAxis(Random.Range(-45f, 45f), Vector3.up)
                * transform.forward;
            TakeDamage(new CommonStruct.AttackInfo
            {
                Position = transform.position + attackDirection * 2.0f,
                Damage = 100
            });
        }
        
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
        StopViewFeedbackTest();
        fsm.Stop();
    }

    private void StopViewFeedbackTest()
    {
        if (testingViewFeedbacks == false)
            return;

        testingViewFeedbacks = false;
        if (viewFeedbacks != null)
        {
            viewFeedbacks.StopFeedbacks();
            viewFeedbacks.RestoreInitialValues();
        }
    }


}
