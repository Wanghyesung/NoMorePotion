using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
             tInputInfo / InputManager
기능 : Inspector의 액션 참조와 활성화 수명을 관리하고 연속 입력을 읽음
       포커스 해제·비활성화 시 입력 취소
 *///////////////////////////////////////////
public struct tInputInfo
{
    public Vector2 MoveDir;
    public Vector2 Delta;
}

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class InputManager : MonoBehaviour
{
    public static InputManager m_Instance { get; private set; }

    [Header("Input Actions")]
    #region InputActions
    [SerializeField] private InputActionReference m_refMoveActionReference;
    [SerializeField] private InputActionReference m_refJumpActionReference;
    [SerializeField] private InputActionReference m_refRollActionReference;
    [SerializeField] private InputActionReference m_refRunActionReference;
    [SerializeField] private InputActionReference m_refDeltaActionReference;
    [SerializeField] private InputActionReference m_refSkill0ActionReference;
    [SerializeField] private InputActionReference m_refAimActionReference;
    [SerializeField] private InputActionReference m_refSwitchHandActionReference;
    [SerializeField] private InputActionReference m_refInteractActionReference;
    [SerializeField] private InputActionReference m_refTakeOutPotionActionReference;

    public InputAction m_refMoveAction { get; private set; }
    public InputAction m_refDeltaAction { get; private set; }
    public InputAction m_refJumpAction { get; private set; }
    public InputAction m_refRollAction { get; private set; }
    public InputAction m_refRunAction { get; private set; }
    public InputAction m_refSkill0Action { get; private set; }
    public InputAction m_refAimAction { get; private set; }
    public InputAction m_refSwitchHandAction { get; private set; }
    public InputAction m_refInteractAction { get; private set; }
    public InputAction m_refTakeOutPotionAction { get; private set; }
    #endregion

    private tInputInfo m_tInputInfo;
    private bool m_bFocused = true;
    public tInputInfo InputInfo => m_tInputInfo;

    public bool HasGameplayInput => m_bFocused && Time.timeScale > 0f;
    public bool IsRunHeld => HasGameplayInput && m_refRunAction.IsPressed();

    private void Awake()
    {
        if (m_Instance != null && m_Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        m_Instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }

    private void Init()
    {
        m_refMoveAction = m_refMoveActionReference.action;
        m_refDeltaAction = m_refDeltaActionReference.action;
        m_refJumpAction = m_refJumpActionReference.action;
        m_refRollAction = m_refRollActionReference.action;
        m_refRunAction = m_refRunActionReference.action;
        m_refSkill0Action = m_refSkill0ActionReference.action;
        m_refAimAction = m_refAimActionReference.action;
        m_refSwitchHandAction = m_refSwitchHandActionReference.action;
        m_refInteractAction = m_refInteractActionReference.action;
        m_refTakeOutPotionAction = m_refTakeOutPotionActionReference.action;
    }

    private void OnEnable()
    {
        if (m_Instance != this)
            return;

        EnableActions();
        m_bFocused = true;
        CancelInput();
    }

    private void Update()
    {
        if (HasGameplayInput == false)
            return;

        m_tInputInfo.MoveDir = m_refMoveAction.ReadValue<Vector2>();
        m_tInputInfo.Delta = m_refDeltaAction.ReadValue<Vector2>();
    }

    private void OnDisable()
    {
        if (m_Instance != this)
            return;

        m_bFocused = false;
        DisableActions();
        CancelInput();
    }

    private void OnDestroy()
    {
        if (m_Instance != this)
            return;

        m_Instance = null;
    }

    //private void OnApplicationFocus(bool _bFocused)
    //{
    //    m_bFocused = _bFocused;
    //    if (_bFocused == false)
    //        CancelInput();
    //}

    private void CancelInput()
    {
        m_tInputInfo = default;
    }

    private void EnableActions()
    {
        m_refSkill0Action.Enable();
        m_refMoveAction.Enable();
        m_refDeltaAction.Enable();
        m_refJumpAction.Enable();
        m_refRollAction.Enable();
        m_refRunAction.Enable();
        m_refAimAction.Enable();
        m_refSwitchHandAction.Enable();
        m_refInteractAction.Enable();
        m_refTakeOutPotionAction.Enable();
    }

    private void DisableActions()
    {
        m_refSkill0Action.Disable();
        m_refMoveAction.Disable();
        m_refDeltaAction.Disable();
        m_refJumpAction.Disable();
        m_refRollAction.Disable();
        m_refRunAction.Disable();
        m_refAimAction.Disable();
        m_refSwitchHandAction.Disable();
        m_refInteractAction.Disable();
        m_refTakeOutPotionAction.Disable();
    }
}
