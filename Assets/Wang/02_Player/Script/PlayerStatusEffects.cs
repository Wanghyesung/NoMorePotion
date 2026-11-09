using UnityEngine;
using Enzeun.Runtime;
using Enzeun.SO;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

// 플레이어 상태 효과의 이벤트 연결, 행동 분기와 시야 연출을 담당한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public sealed class PlayerStatusEffects : MonoBehaviour
{
    [SerializeField] private VisionEffect visionEffect;
    private FSM fsm;
    private Player player;
    private Hand hand;
    private bool thirsty;
    private BuffContainer playerBuffContainer;
#if UNITY_EDITOR
    private StatusEffectData testWhat;
    private StatusEffectData testConfusion;
    private StatusEffectData testThirst;
    private bool testStrongVision;
    private readonly object testWhatSource = new object();
    private readonly object testConfusionSource = new object();
    private readonly object testThirstSource = new object();
#endif

    private void Awake()
    {
        fsm = GetComponent<FSM>();
        player = GetComponent<Player>();
        hand = GetComponent<Hand>();
#if UNITY_EDITOR
        testWhat = Resources.Load<StatusEffectData>("ScriptableObjects/StatusEffects/61014");
        testConfusion = Resources.Load<StatusEffectData>("ScriptableObjects/StatusEffects/61003");
        testThirst = Resources.Load<StatusEffectData>("ScriptableObjects/StatusEffects/61004");
#endif
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
        hand.OnPotionHeld += OnPotionHeld;
        // 첫 활성화는 Start에서 컨테이너를 받은 뒤 연결하고, 이후 재활성화는 여기서 연결한다.
        SubscribeBuffEvents();
    }

#if UNITY_EDITOR
    private void Update()
    {
        bool holdVision = Keyboard.current != null && Application.isFocused == true
            && InputManager.Instance != null && InputManager.Instance.HasGameplayInput == true
            && (Keyboard.current.digit6Key.isPressed == true || Keyboard.current.numpad6Key.isPressed == true);

        if (testStrongVision != holdVision)
        {
            testStrongVision = holdVision;
            RefreshBuffVision();
        }

        if (playerBuffContainer == null || Keyboard.current == null)
            return;

        // 윗줄 숫자키와 숫자패드 모두 지원한다. 누를 때 한 번만 적용/해제한다.
        if (Keyboard.current.digit0Key.wasPressedThisFrame == true || Keyboard.current.numpad0Key.wasPressedThisFrame == true)
            ToggleTestEffect(testThirst, testThirstSource);
        if (Keyboard.current.digit7Key.wasPressedThisFrame == true || Keyboard.current.numpad7Key.wasPressedThisFrame == true)
            ToggleTestEffect(testWhat, testWhatSource);
        if (Keyboard.current.digit8Key.wasPressedThisFrame == true || Keyboard.current.numpad8Key.wasPressedThisFrame == true)
            ToggleTestEffect(testConfusion, testConfusionSource);
        // 9: 추위 정지 동작을 즉시 1초 동안 테스트한다(버프 자체는 추가하지 않음).
        if (Keyboard.current.digit9Key.wasPressedThisFrame == true || Keyboard.current.numpad9Key.wasPressedThisFrame == true)
            fsm.Freeze(1f);
    }

    private void ToggleTestEffect(StatusEffectData data, object source)
    {
        if (data == null)
        {
            Debug.LogWarning("테스트용 상태 효과 SO를 찾지 못했습니다.", this);
            return;
        }

        // 동일 상태라도 포션·생존 수치가 건 효과는 제거하지 않는다.
        if (playerBuffContainer.RemoveBySource(source) > 0)
        {
            Debug.Log($"[상태 테스트] {data.statusEffectName} 해제", this);
            return;
        }

        if (playerBuffContainer.Apply(data, source) == true)
            Debug.Log($"[상태 테스트] {data.statusEffectName} 적용", this);
    }
#endif

    private void OnDisable()
    {
        hand.OnPotionHeld -= OnPotionHeld;
        thirsty = false;
#if UNITY_EDITOR
        testStrongVision = false;
#endif

        // 플레이어에 반영한 값만 복구한다. 컨테이너의 효과는 유지하며 재활성화 때 다시 읽는다.
        fsm.ReverseMovement = false;
        fsm.MoveSpeedMultiplier = 1f;
        player.ConfusePotionUse = false;
        if (playerBuffContainer != null)
        {
            playerBuffContainer.OnEffectAdded -= OnBuffAdded;
            playerBuffContainer.OnEffectRemoved -= OnBuffRemoved;
            playerBuffContainer.OnPeriodicAction -= OnBuffPeriodicAction;
#if UNITY_EDITOR
            playerBuffContainer.RemoveBySource(testWhatSource);
            playerBuffContainer.RemoveBySource(testConfusionSource);
            playerBuffContainer.RemoveBySource(testThirstSource);
#endif
        }
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
        // 구독 전에 이미 걸린 효과도 반영한다. 다른 컴포넌트의 Start 실행 순서에 의존하지 않는다.
        RefreshMovementReversal();
        RefreshPotionUseConfusion();
        RefreshMoveSpeed();
        RefreshBuffVision();
        RefreshThirst();
    }

    /*//////////////////////////////////////////
                상태이상
    //////////////////////////////////////////*/

    // 상태 효과가 추가되면 이동 반전, 이동속도와 시야를 갱신한다.
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
            case 61003: // 혼란
            case 61009: // 죽어라
                RefreshMovementReversal();
                RefreshPotionUseConfusion();
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
                break;
            case 61004: // 갈증
                RefreshThirst();
                break;
            case 61006: // 허기: 주기 행동 이벤트에서 처리
                break;
            case 61005: // 탈수
            case 61007: // 굶주림: 체력 틱은 컨테이너 담당
                break;
            case 61014: // 뭐라고?: 던지기/마시기 변경
                RefreshPotionUseConfusion();
                break;
        }

        // ID별 분기와 무관하게 스탯 모듈을 반영한다. 제거 후에도 다른 주체의 효과가 남을 수 있다.
        RefreshMoveSpeed();
        RefreshBuffVision();
    }

    private void RefreshPotionUseConfusion()
    {
        player.ConfusePotionUse = false;
        if (playerBuffContainer == null)
            return;

        for (int i = 0; i < playerBuffContainer.debuffs.Count; i++)
        {
            int id = playerBuffContainer.debuffs[i].data.ID;
            // 61014=뭐라고?, 61003=혼란, 61009=죽어라. 상위 디버프를 단독 적용해도 혼동을 포함한다.
            if (id == 61014 || id == 61003 || id == 61009)
            {
                player.ConfusePotionUse = true;
                return;
            }
        }
    }

    private void RefreshMoveSpeed()
    {
        if (playerBuffContainer == null)
            return;

        // 1=기본 속도, 0.7=30% 감소. 갱신은 이벤트에서만 하고 걷기 상태가 이 값을 사용한다.
        fsm.MoveSpeedMultiplier = playerBuffContainer.GetStatMultiplier(StatType.MoveSpeed);
    }

    private void RefreshMovementReversal()
    {
        fsm.ReverseMovement = false;
        if (playerBuffContainer == null)
            return;

        // 같은 효과를 건 다른 주체나 다른 반전 디버프가 남아 있으면 유지한다.
        for (int i = 0; i < playerBuffContainer.debuffs.Count; i++)
        {
            int id = playerBuffContainer.debuffs[i].data.ID;
            // 61003=혼란, 61009=죽어라. 하나라도 남아 있으면 이동 반전을 유지한다.
            if (id == 61003 || id == 61009)
            {
                fsm.ReverseMovement = true;
                return;
            }
        }
    }

    private void RefreshBuffVision()
    {
        if (visionEffect == null)
            return;

        // 시야 배율 0.8 → 비네트 강도 0.2. 화면 면적을 정확히 20% 가리는 값은 아니다.
        float reduction = playerBuffContainer != null
            ? Mathf.Clamp01(1f - playerBuffContainer.GetStatMultiplier(StatType.Vision)) : 0f;
#if UNITY_EDITOR
        // 6을 누르는 동안만 강도 0.8 이상을 유지한다. 떼면 남아 있는 버프의 강도로 복구한다.
        if (testStrongVision == true)
            reduction = Mathf.Max(reduction, 0.8f);
#endif
        if (reduction > 0f)
            visionEffect.Play(reduction);
        else
            visionEffect.Stop();
    }

    // 주기는 컨테이너가, 한 번 얼었을 때의 정지 시간은 FSM이 관리한다.
    private void OnBuffPeriodicAction(ActiveStatusEffect effect, PeriodicActionModule module)
    {
        switch (module.ActionType)
        {
            case PeriodicActionType.StopPlayerMove: // 추위: 일시 이동 정지
                fsm.Freeze(module.DurationSeconds);
                break;
            case PeriodicActionType.ConsumeFirstItem: // 허기: 첫 아이템 소모
                break;

        }
    }


    private void RefreshThirst()
    {
        thirsty = false;
        for (int i = 0; i < playerBuffContainer.debuffs.Count; i++)
        {
            if (playerBuffContainer.debuffs[i].data.ID == 61004)
            {
                thirsty = true;
                break;
            }
        }

        // 갈증이 갱신될 때 이미 들고 있는 포션도 오른손부터 한 개 마신다.
        OnPotionHeld();
    }

    // 갈증 갱신 또는 포션 획득 때 호출한다. 일반 마시기 입력과는 별개다.
    private void OnPotionHeld()
    {
        if (thirsty == false || player.Character.HP <= 0 || playerBuffContainer.HasAuthority == false)
            return;

        Hand.Side side = hand.GetPotion(Hand.Side.Right) != null ? Hand.Side.Right : Hand.Side.Left;
        if (hand.GetPotion(side) == null)
            return;

        Hand.Side previousSide = hand.FocusedSide;
        hand.Focus(side);
        // 소비는 Drink 상태의 첫 애니메이션 이벤트에서 처리한다.
        if (fsm.ChangeActionState(FSM.StateType.Drink) == false)
            hand.Focus(previousSide);
    }
}
