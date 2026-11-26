using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/*///////////////////////////////////////////
             PlayerInputInfo / InputManager
기능 : Inspector의 액션 참조와 활성화 수명을 관리하고 연속 입력을 읽음
 *///////////////////////////////////////////
public struct PlayerInputInfo
{
    public Vector2 MoveDir;
    public Vector2 Delta;
}

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class InputManager : MonoBehaviour
{
    [System.Flags]
    public enum GameplayKeys
    {
        None = 0,
        Move = 1 << 0,
        Look = 1 << 1,
        Jump = 1 << 2,
        Roll = 1 << 3,
        UsePotion = 1 << 4,
        Aim = 1 << 5,
        SwitchHand = 1 << 6,
        Interact = 1 << 7,
        TakePotion = 1 << 8,
        Shake = 1 << 9,
        QuickSlot = 1 << 10,
        QuickSlot1 = 1 << 11,
        QuickSlot4 = 1 << 12,
        ThrowPotion = 1 << 13,
        DrinkPotion = 1 << 14,
        Tab = 1 << 15,
        All = (1 << 16) - 1
    }

    public static InputManager Instance { get; private set; }
    public GameplayKeys GuideAllowedKeys { get; private set; } = ExpandPotionKeys(GameplayKeys.All);
    public bool CanThrowPotion => (GuideAllowedKeys & GameplayKeys.ThrowPotion) != 0;
    public bool CanDrinkPotion => (GuideAllowedKeys & GameplayKeys.DrinkPotion) != 0;

    [Header("Input Actions")]
    #region InputActions
    [SerializeField] private InputActionReference moveActionReference;
    [SerializeField] private InputActionReference jumpActionReference;
    [SerializeField] private InputActionReference rollActionReference;
    [SerializeField] private InputActionReference deltaActionReference;
    [SerializeField] private InputActionReference skill0ActionReference;
    [SerializeField] private InputActionReference aimActionReference;
    [SerializeField] private InputActionReference switchHandActionReference;
    [SerializeField] private InputActionReference interactActionReference;
    [SerializeField] private InputActionReference takeOutPotionActionReference;
    [SerializeField] private InputActionReference shakeActionReference;
    [SerializeField] private InputActionReference number0ActionReference;
    [SerializeField] private InputActionReference number1ActionReference;
    [SerializeField] private InputActionReference number2ActionReference;
    [SerializeField] private InputActionReference number3ActionReference;
    [SerializeField] private InputActionReference number4ActionReference;
    [SerializeField] private InputActionReference number5ActionReference;
    [SerializeField] private InputActionReference number6ActionReference;
    [SerializeField] private InputActionReference number7ActionReference;
    [SerializeField] private InputActionReference number8ActionReference;
    [SerializeField] private InputActionReference number9ActionReference;
    [SerializeField] private InputActionReference escapeActionReference;
    [SerializeField] private InputActionReference altActionReference;
    [SerializeField] private InputActionReference tabActionReference;

    public InputAction MoveAction { get; private set; }
    public InputAction DeltaAction { get; private set; }
    public InputAction JumpAction { get; private set; }
    public InputAction RollAction { get; private set; }
    public InputAction Skill0Action { get; private set; }
    public InputAction AimAction { get; private set; }
    public InputAction SwitchHandAction { get; private set; }
    public InputAction InteractAction { get; private set; }
    public InputAction TakeOutPotionAction { get; private set; }
    public InputAction ShakeAction { get; private set; }
    public InputAction Number0Action { get; private set; }
    public InputAction Number1Action { get; private set; }
    public InputAction Number2Action { get; private set; }
    public InputAction Number3Action { get; private set; }
    public InputAction Number4Action { get; private set; }
    public InputAction Number5Action { get; private set; }
    public InputAction Number6Action { get; private set; }
    public InputAction Number7Action { get; private set; }
    public InputAction Number8Action { get; private set; }
    public InputAction Number9Action { get; private set; }
    public InputAction EscapeAction { get; private set; }
    public InputAction AltAction { get; private set; }
    public InputAction TabAction { get; private set; }
    #endregion

    private PlayerInputInfo inputInfo;
    private bool focused = true;
    private bool cursorLocked;
    private bool respawnCursor;
    private bool requestedCursorVisible;
    private bool gameplayInputEnabled = true;
    public PlayerInputInfo InputInfo => inputInfo;

    public bool HasGameplayInput => gameplayInputEnabled && focused && Time.timeScale > 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void Init()
    {
        MoveAction = moveActionReference.action;
        DeltaAction = deltaActionReference.action;
        JumpAction = jumpActionReference.action;
        RollAction = rollActionReference.action;
        Skill0Action = skill0ActionReference.action;
        AimAction = aimActionReference.action;
        SwitchHandAction = switchHandActionReference.action;
        InteractAction = interactActionReference.action;
        TakeOutPotionAction = takeOutPotionActionReference.action;
        ShakeAction = shakeActionReference.action;
        Number0Action = number0ActionReference.action;
        Number1Action = number1ActionReference.action;
        Number2Action = number2ActionReference.action;
        Number3Action = number3ActionReference.action;
        Number4Action = number4ActionReference.action;
        Number5Action = number5ActionReference.action;
        Number6Action = number6ActionReference.action;
        Number7Action = number7ActionReference.action;
        Number8Action = number8ActionReference.action;
        Number9Action = number9ActionReference.action;
        EscapeAction = escapeActionReference.action;
        AltAction = altActionReference.action;
        TabAction = tabActionReference.action;
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        EscapeAction.Enable();
        SceneManager.sceneLoaded += OnSceneLoaded;
        // 인벤토리에서 플레이어 조작이 꺼져도 Alt 해제 이벤트는 받는다.
        AltAction.performed += OnAltPressed;
        AltAction.canceled += OnAltReleased;
        AltAction.Enable();

        if (gameplayInputEnabled == true)
            EnableActions();
        focused = true;
        CancelInput();

        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        if (HasGameplayInput == false)
            return;

        inputInfo.MoveDir = MoveAction.ReadValue<Vector2>();
        inputInfo.Delta = DeltaAction.ReadValue<Vector2>();
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        focused = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EscapeAction.Disable();
        AltAction.performed -= OnAltPressed;
        AltAction.canceled -= OnAltReleased;
        AltAction.Disable();
        TabAction.Disable();
        DisableActions();
        CancelInput();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        bool isLobby = scene.name == SceneNames.OUT_GAME;
        cursorLocked = false;
        respawnCursor = false;
        SetInputEnabled(!isLobby);
        if (isLobby)
        {
            AltAction.Disable();
            UnLockAllKeys();
            TabAction.Disable();
        }
        else
        {
            AltAction.Enable();
            UnLockKeys(GameplayKeys.Tab);
        }

        RefreshCursorState(isLobby || AltAction.IsPressed());
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (Instance != this || isActiveAndEnabled == false)
            return;

        focused = hasFocus;
        CancelInput();

        RefreshCursorState(!hasFocus || cursorLocked || AltAction.IsPressed());
    }

    private void OnAltPressed(InputAction.CallbackContext context)
    {
        ShowCursor(true);
    }

    private void OnAltReleased(InputAction.CallbackContext context)
    {
        ShowCursor(false);
    }

    /// <summary>커서 잠금이 해제되어 있으면 커서를 표시합니다.</summary>
    public void FocusCursor() => ShowCursor(true);

    /// <summary>커서 잠금 상태를 바꾸지 않고 Alt 입력에 따른 표시로 돌아갑니다.</summary>
    public void UnfocusCursor() => ShowCursor(AltAction.IsPressed());

    public void ShowCursor(bool show)
    {
        if (cursorLocked == true)
            return;

        RefreshCursorState(show);
    }

    /// <summary>플레이어 조작만 켜거나 끕니다. Tab과 Escape 등 UI 입력은 유지합니다.</summary>
    public void SetInputEnabled(bool inputEnabled)
    {
        gameplayInputEnabled = inputEnabled;

        if (inputEnabled == true && isActiveAndEnabled == true)
            EnableActions();
        else
            DisableActions();

        CancelInput();
    }

    /// <summary>인벤토리에서 커서 표시를 고정합니다. 해제하면 Alt 입력에 따른 표시로 돌아갑니다.</summary>
    public void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;
        RefreshCursorState(locked || AltAction.IsPressed());
    }

    // 부활 팝업이 닫히면 인벤토리와 Alt 입력이 마지막으로 요청한 상태를 다시 적용한다.
    public void SetRespawnCursor(bool visible)
    {
        respawnCursor = visible;
        RefreshCursorState(requestedCursorVisible);
    }

    private void RefreshCursorState(bool bState)
    {
        requestedCursorVisible = bState;
        bState |= respawnCursor || SceneManager.GetActiveScene().name == SceneNames.OUT_GAME;
        Cursor.lockState = bState ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = bState;

        if (gameplayInputEnabled && isActiveAndEnabled)
        {
            ApplyGuideKey(Skill0Action, GameplayKeys.ThrowPotion | GameplayKeys.DrinkPotion);
            ApplyGuideKey(AimAction, GameplayKeys.Aim);
        }
    }

    private void CancelInput()
    {
        inputInfo = default;
    }

    private void EnableActions()
    {
        ApplyGuideKey(Skill0Action, GameplayKeys.ThrowPotion | GameplayKeys.DrinkPotion);
        ApplyGuideKey(MoveAction, GameplayKeys.Move);
        ApplyGuideKey(DeltaAction, GameplayKeys.Look);
        ApplyGuideKey(JumpAction, GameplayKeys.Jump);
        ApplyGuideKey(RollAction, GameplayKeys.Roll);
        ApplyGuideKey(AimAction, GameplayKeys.Aim);
        ApplyGuideKey(SwitchHandAction, GameplayKeys.SwitchHand);
        ApplyGuideKey(InteractAction, GameplayKeys.Interact);
        ApplyGuideKey(TakeOutPotionAction, GameplayKeys.TakePotion);
        ApplyGuideKey(ShakeAction, GameplayKeys.Shake);
        ApplyGuideKey(Number0Action, GameplayKeys.QuickSlot);
        // 전체 퀵슬롯 또는 이 번호만 허용한 단계에서 활성화한다.

        ApplyGuideKey(Number1Action, GameplayKeys.QuickSlot | GameplayKeys.QuickSlot1);
        ApplyGuideKey(Number2Action, GameplayKeys.QuickSlot);
        ApplyGuideKey(Number3Action, GameplayKeys.QuickSlot);
        ApplyGuideKey(Number4Action, GameplayKeys.QuickSlot | GameplayKeys.QuickSlot4);
        ApplyGuideKey(Number5Action, GameplayKeys.QuickSlot);
        ApplyGuideKey(Number6Action, GameplayKeys.QuickSlot);
        ApplyGuideKey(Number7Action, GameplayKeys.QuickSlot);
        ApplyGuideKey(Number8Action, GameplayKeys.QuickSlot);
        ApplyGuideKey(Number9Action, GameplayKeys.QuickSlot);
    }

    private void ApplyGuideKey(InputAction action, GameplayKeys key)
    {
        bool cursorBlocksPotion = Cursor.visible && (key & (GameplayKeys.ThrowPotion | GameplayKeys.DrinkPotion | GameplayKeys.Aim)) != 0;
        bool aimBlocked = key == GameplayKeys.Aim && !CanThrowPotion;
        if ((GuideAllowedKeys & key) != 0 && !cursorBlocksPotion && !aimBlocked)
            action.Enable();
        else
            action.Disable();
    }

    public void SetGuideAllowedKeys(GameplayKeys keys)
    {
        GuideAllowedKeys = ExpandPotionKeys(keys);
        // 인벤토리가 열려 플레이어 입력이 꺼져도 Tab 잠금은 별도로 적용한다.
        if (isActiveAndEnabled)
            ApplyGuideKey(TabAction, GameplayKeys.Tab);
        else
            TabAction.Disable();
        if (gameplayInputEnabled && isActiveAndEnabled)
            EnableActions();
        else
            DisableActions();
        CancelInput();
    }

    // 기존 씬에 저장된 UsePotion 값은 던지기와 마시기를 함께 허용하는 의미로 유지한다.
    private static GameplayKeys ExpandPotionKeys(GameplayKeys keys)
    {
        if ((keys & GameplayKeys.UsePotion) != 0)
            keys = (keys & ~GameplayKeys.UsePotion) | GameplayKeys.ThrowPotion | GameplayKeys.DrinkPotion;
        return keys;
    }

    public void LockKeys(GameplayKeys keys) => SetGuideAllowedKeys(GuideAllowedKeys & ~ExpandPotionKeys(keys));
    public void UnLockKeys(GameplayKeys keys) => SetGuideAllowedKeys(GuideAllowedKeys | keys);
    public void LockAllKeys() => SetGuideAllowedKeys(GameplayKeys.None);
    public void UnLockAllKeys() => SetGuideAllowedKeys(GameplayKeys.All);
    public void LockMoveKey() => LockKeys(GameplayKeys.Move);
    public void UnLockMoveKey() => UnLockKeys(GameplayKeys.Move);

    private void DisableActions()
    {
        Skill0Action.Disable();
        MoveAction.Disable();
        DeltaAction.Disable();
        JumpAction.Disable();
        RollAction.Disable();
        AimAction.Disable();
        SwitchHandAction.Disable();
        InteractAction.Disable();
        TakeOutPotionAction.Disable();
        ShakeAction.Disable();
        Number0Action.Disable();
        Number1Action.Disable();
        Number2Action.Disable();
        Number3Action.Disable();
        Number4Action.Disable();
        Number5Action.Disable();
        Number6Action.Disable();
        Number7Action.Disable();
        Number8Action.Disable();
        Number9Action.Disable();
    }
}
