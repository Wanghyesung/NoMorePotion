using UnityEngine;
using UnityEngine.InputSystem;

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
    public static InputManager Instance { get; private set; }

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
    #endregion

    private PlayerInputInfo inputInfo;
    private bool focused = true;
    private bool cursorLocked = true;
    private bool cursorCaptured;
    private bool altCursorActive;
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
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        EscapeAction.performed += ReleaseCursor;
        EscapeAction.Enable();
        // 인벤토리에서 플레이어 조작이 꺼져도 Alt 해제 이벤트는 받는다.
        AltAction.performed += OnAltPressed;
        AltAction.canceled += OnAltReleased;
        AltAction.Enable();

        if (gameplayInputEnabled == true)
            EnableActions();
        focused = true;
        CancelInput();
        SetCursorLocked(cursorLocked);
    }

    private void Update()
    {
        if (HasGameplayInput == false)
            return;

        inputInfo.MoveDir = MoveAction.ReadValue<Vector2>();
        inputInfo.Delta = cursorCaptured == true ? DeltaAction.ReadValue<Vector2>() : Vector2.zero;
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        focused = false;
        if (altCursorActive == true)
        {
            altCursorActive = false;
            cursorLocked = true;
        }

        EscapeAction.performed -= ReleaseCursor;
        EscapeAction.Disable();
        AltAction.performed -= OnAltPressed;
        AltAction.canceled -= OnAltReleased;
        AltAction.Disable();
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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (Instance != this || isActiveAndEnabled == false)
            return;

        focused = hasFocus;
        // 창 밖에서 Alt를 떼어 해제 입력을 놓쳐도 커서 상태가 남지 않게 한다.
        if (hasFocus == false && altCursorActive)
        {
            altCursorActive = false;
            cursorLocked = true;
        }
        CancelInput();

        SetCursorLocked(cursorLocked);
    }

    private void ReleaseCursor(InputAction.CallbackContext context) => SetCursorLocked(false);

    private void OnAltPressed(InputAction.CallbackContext context)
    {
        altCursorActive = true;
        FocusCursor();
    }

    private void OnAltReleased(InputAction.CallbackContext context)
    {
        altCursorActive = false;
        UnfocusCursor();
    }

    /// <summary>커서를 표시하고 마우스 시점 입력을 멈춥니다.</summary>
    public void FocusCursor() => SetCursorLocked(false);

    /// <summary>커서를 숨깁니다. 인벤토리 등으로 조작이 꺼져 있으면 표시는 유지합니다.</summary>
    public void UnfocusCursor() => SetCursorLocked(true);

    /// <summary>플레이어 조작만 켜거나 끕니다. UI와 Escape 입력은 유지합니다.</summary>
    public void SetInputEnabled(bool inputEnabled)
    {
        gameplayInputEnabled = inputEnabled;

        if (inputEnabled == true && isActiveAndEnabled == true)
            EnableActions();
        else
            DisableActions();

        CancelInput();
        RefreshCursorState();
    }

    /// <summary>커서 잠금과 표시를 변경합니다. false면 마우스 시점 입력도 멈춥니다.</summary>
    public void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;
        inputInfo.Delta = Vector2.zero;
        RefreshCursorState();
    }

    private void RefreshCursorState()
    {
        // UI를 닫아도 Alt 해제 입력 전까지는 표시하고, UI가 열려 있으면 항상 표시한다.
        bool capture = focused == true && cursorLocked == true  && altCursorActive == false;

        if (cursorCaptured != capture)
            inputInfo.Delta = Vector2.zero;
        cursorCaptured = capture;

        CursorLockMode lockMode = capture ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != lockMode)
            Cursor.lockState = lockMode;
        if (Cursor.visible != !capture)
            Cursor.visible = !capture;
    }

    private void CancelInput()
    {
        inputInfo = default;
    }

    private void EnableActions()
    {
        Skill0Action.Enable();
        MoveAction.Enable();
        DeltaAction.Enable();
        JumpAction.Enable();
        RollAction.Enable();
        AimAction.Enable();
        SwitchHandAction.Enable();
        InteractAction.Enable();
        TakeOutPotionAction.Enable();
        ShakeAction.Enable();
        Number0Action.Enable();
        Number1Action.Enable();
        Number2Action.Enable();
        Number3Action.Enable();
        Number4Action.Enable();
        Number5Action.Enable();
        Number6Action.Enable();
        Number7Action.Enable();
        Number8Action.Enable();
        Number9Action.Enable();
    }

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
