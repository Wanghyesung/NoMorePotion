using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;
using static InputManager;
using Enzeun.Runtime;

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
    private MMF_Player viewFeedbacks;
    private bool testingViewFeedbacks;

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

        bool testHit = Input.GetKeyDown(KeyCode.O);
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
                Damage = 10,
                AttackPower = 10
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

    /*//////////////////////////////////////////
                상태이상 <나중에 컴포넌트화>
    //////////////////////////////////////////*/

    // 수분 상태 변경: 정상, 촉촉함, 갈증, 탈수. oldState는 이전 상태, newState는 변경된 상태.
    private void ChangeHydration(HydrationState oldState, HydrationState newState)
    {

    }

    // 체온 상태 변경: 정상, 추움, 더움. 이전 상태의 효과를 해제하고 새 상태의 효과를 적용할 위치.
    private void ChangeBodyTemperature(BodyTemperatureState oldState, BodyTemperatureState newState)
    {

    }

    // 정신력 상태 변경: 정상, 똑똑이, 뭐라고?, 혼란, 죽어라. 정신력 0의 사망 처리는 별도 함수에서 담당.
    private void ChangeMentalState(MentalState oldState, MentalState newState)
    {

    }

    // 포만감 상태 변경: 정상, 배부름, 허기, 굶주림. 이전 상태의 효과를 해제하고 새 상태의 효과를 적용할 위치.
    private void ChangeSatiation(SatiationState oldState, SatiationState newState)
    {

    }

    // 정신력이 0 이하가 되었을 때의 사망 처리를 추가할 위치. 현재 이벤트에는 연결하지 않음.
    private void OnDeathThresholdReached()
    {

    }


}
