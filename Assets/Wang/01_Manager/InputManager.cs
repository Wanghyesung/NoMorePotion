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
    [SerializeField] private InputActionReference number1ActionReference;
    [SerializeField] private InputActionReference number2ActionReference;
    [SerializeField] private InputActionReference number3ActionReference;
    [SerializeField] private InputActionReference number4ActionReference;
    [SerializeField] private InputActionReference number5ActionReference;
    [SerializeField] private InputActionReference number6ActionReference;
    [SerializeField] private InputActionReference escapeActionReference;

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
    public InputAction Number1Action { get; private set; }
    public InputAction Number2Action { get; private set; }
    public InputAction Number3Action { get; private set; }
    public InputAction Number4Action { get; private set; }
    public InputAction Number5Action { get; private set; }
    public InputAction Number6Action { get; private set; }
    public InputAction EscapeAction { get; private set; }
    #endregion

    private PlayerInputInfo inputInfo;
    private bool focused = true;
    private bool cursorLocked = true;
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
        Number1Action = number1ActionReference.action;
        Number2Action = number2ActionReference.action;
        Number3Action = number3ActionReference.action;
        Number4Action = number4ActionReference.action;
        Number5Action = number5ActionReference.action;
        Number6Action = number6ActionReference.action;
        EscapeAction = escapeActionReference.action;
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        EscapeAction.performed += ReleaseCursor;
        EscapeAction.Enable();
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
        inputInfo.Delta = cursorLocked == true ? DeltaAction.ReadValue<Vector2>() : Vector2.zero;
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        focused = false;
        EscapeAction.performed -= ReleaseCursor;
        EscapeAction.Disable();
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
        CancelInput();

        SetCursorLocked(cursorLocked);
    }

    private void ReleaseCursor(InputAction.CallbackContext context) => SetCursorLocked(false);

    /// <summary>플레이어 조작만 켜거나 끕니다. UI와 Escape 입력은 유지합니다.</summary>
    public void SetInputEnabled(bool inputEnabled)
    {
        gameplayInputEnabled = inputEnabled;

        if (inputEnabled == true && isActiveAndEnabled == true)
            EnableActions();
        else
            DisableActions();

        CancelInput();
    }

    /// <summary>커서 잠금과 표시를 변경합니다. false면 마우스 시점 입력도 멈춥니다.</summary>
    public void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;
        inputInfo.Delta = Vector2.zero;
        bool capture = focused == true && cursorLocked == true;
        Cursor.lockState = capture == true ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = capture == false;
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
        Number1Action.Enable();
        Number2Action.Enable();
        Number3Action.Enable();
        Number4Action.Enable();
        Number5Action.Enable();
        Number6Action.Enable();
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
        Number1Action.Disable();
        Number2Action.Disable();
        Number3Action.Disable();
        Number4Action.Disable();
        Number5Action.Disable();
        Number6Action.Disable();
    }
}
