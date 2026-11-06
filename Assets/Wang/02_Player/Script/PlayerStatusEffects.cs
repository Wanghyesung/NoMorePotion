using UnityEngine;
using Enzeun.Runtime;
using Enzeun.SO;

// 플레이어 상태 효과의 이벤트 연결, 행동 분기와 시야 연출을 담당한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public sealed class PlayerStatusEffects : MonoBehaviour
{
    [SerializeField] private VisionEffect visionEffect;
    [SerializeField] private StatusEffectData testBuff;
    private BuffContainer playerBuffContainer;
    private bool testingBuff;

    private void Awake()
    {
        if (visionEffect == null)
            visionEffect = FindFirstObjectByType<VisionEffect>();
    }

    private void Start()
    {
        if (SurviveStatusManager.Instance == null)
            return;

        playerBuffContainer = SurviveStatusManager.Instance.PlayerBuffContainer;
        SubscribeBuffEvents();
    }

    private void OnEnable()
    {
        SubscribeBuffEvents();
    }

    // 임시 테스트: P를 누를 때마다 테스트 버프를 적용/해제한다.
    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.P))
        //    ToggleTestBuff();
    }

    private void ToggleTestBuff()
    {
        if (playerBuffContainer == null || testBuff == null)
            return;

        if (testingBuff == true)
        {
            playerBuffContainer.RemoveBySource(this);
            testingBuff = false;
        }
        else
        {
            testingBuff = playerBuffContainer.Apply(testBuff, this);
        }
    }

    private void OnDisable()
    {
        if (playerBuffContainer != null)
        {
            playerBuffContainer.OnEffectAdded -= OnBuffAdded;
            playerBuffContainer.OnEffectRemoved -= OnBuffRemoved;
            playerBuffContainer.OnPeriodicAction -= OnBuffPeriodicAction;
            if (testingBuff)
                playerBuffContainer.RemoveBySource(this);
        }
        testingBuff = false;
        if (visionEffect != null)
            visionEffect.Stop();
    }

    private void SubscribeBuffEvents()
    {
        if (playerBuffContainer == null)
            return;

        playerBuffContainer.OnEffectAdded += OnBuffAdded;
        playerBuffContainer.OnEffectRemoved += OnBuffRemoved;
        playerBuffContainer.OnPeriodicAction += OnBuffPeriodicAction;
        RefreshBuffVision();
    }

    /*//////////////////////////////////////////
                상태이상
    //////////////////////////////////////////*/

    // 시야 이외의 효과는 분기만 준비한다.
    private void OnBuffAdded(ActiveStatusEffect effect)
    {
        HandleBuffChanged(effect);
    }

    // 만료 또는 명시적 제거로 효과가 빠졌을 때 호출된다.
    private void OnBuffRemoved(ActiveStatusEffect effect)
    {
        HandleBuffChanged(effect);
    }

    private void HandleBuffChanged(ActiveStatusEffect effect)
    {
        switch (effect.data.ID)
        {
            case 61003: // 혼란: 방향키 반전은 추후 연결
            case 61009: // 죽어라: 이동속도 감소는 추후 연결
                break;
            case 60001: // 똑똑이
            case 61010: // 멍청함: 제조 확률
                break;
            case 60002: // 촉촉함
                break;
            case 60003: // 빠른발
            case 61001: // 더위
            case 61008: // 배부름
            case 61011: // 느림보: 이동속도
                break;
            case 60004: // 튼튼이
            case 61012: // 허약이: 공격력
                break;
            case 60005: // 단단이
            case 61013: // 물렁이: 방어력
                break;
            case 60006: // 차분함
            case 60007: // 열혈: 체온 제한
                break;
            case 61002: // 추위
            case 61004: // 갈증
            case 61006: // 허기: 주기 행동 이벤트에서 처리
                break;
            case 61005: // 탈수
            case 61007: // 굶주림: 체력 틱은 컨테이너 담당
                break;
            case 61014: // 뭐라고?: 던지기/마시기 변경
                break;
        }

        // 제거된 효과만 보고 복구하지 않고, 다른 주체의 효과까지 포함해 재계산한다.
        RefreshBuffVision();
    }

    private void RefreshBuffVision()
    {
        if (visionEffect == null || playerBuffContainer == null)
            return;

        float reduction = Mathf.Clamp01(1f - playerBuffContainer.GetStatMultiplier(StatType.Vision));
        if (reduction > 0f)
            visionEffect.Play(reduction);
        else
            visionEffect.Stop();
    }

    // 추위의 이동 정지, 허기의 아이템 소모 등 주기 행동의 발동 알림을 받는다.
    private void OnBuffPeriodicAction(ActiveStatusEffect effect, PeriodicActionModule module)
    {
        switch (module.ActionType)
        {
            case PeriodicActionType.StopPlayerMove: // 추위: 일시 이동 정지
                break;
            case PeriodicActionType.ConsumeFirstItem: // 허기: 첫 아이템 소모
                break;
            case PeriodicActionType.DrinkHeldPotion: // 갈증: 보유 포션 마시기
                break;
        }
    }


}
