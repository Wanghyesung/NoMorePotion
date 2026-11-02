using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
             PlayerInputInfo / InputManager
기능 : Inspector의 액션 참조와 활성화 수명을 관리하고 연속 입력을 읽음
       포커스 해제·비활성화 시 입력 취소
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
    [UnityEngine.Serialization.FormerlySerializedAs("m_refMoveActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refMoveActionReference")]
    [SerializeField] private InputActionReference moveActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refJumpActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refJumpActionReference")]
    [SerializeField] private InputActionReference jumpActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refRollActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refRollActionReference")]
    [SerializeField] private InputActionReference rollActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refDeltaActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refDeltaActionReference")]
    [SerializeField] private InputActionReference deltaActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refSkill0ActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refSkill0ActionReference")]
    [SerializeField] private InputActionReference skill0ActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refAimActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refAimActionReference")]
    [SerializeField] private InputActionReference aimActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refSwitchHandActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refSwitchHandActionReference")]
    [SerializeField] private InputActionReference switchHandActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refInteractActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refInteractActionReference")]
    [SerializeField] private InputActionReference interactActionReference;
    [UnityEngine.Serialization.FormerlySerializedAs("m_refTakeOutPotionActionReference")]
    [UnityEngine.Serialization.FormerlySerializedAs("refTakeOutPotionActionReference")]
    [SerializeField] private InputActionReference takeOutPotionActionReference;

    public InputAction MoveAction { get; private set; }
    public InputAction DeltaAction { get; private set; }
    public InputAction JumpAction { get; private set; }
    public InputAction RollAction { get; private set; }
    public InputAction Skill0Action { get; private set; }
    public InputAction AimAction { get; private set; }
    public InputAction SwitchHandAction { get; private set; }
    public InputAction InteractAction { get; private set; }
    public InputAction TakeOutPotionAction { get; private set; }
    #endregion

    private PlayerInputInfo inputInfo;
    private bool focused = true;
    public PlayerInputInfo InputInfo => inputInfo;

    public bool HasGameplayInput => focused && Time.timeScale > 0f;

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
        init();
    }

    private void init()
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
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        enableActions();
        focused = true;
        cancelInput();
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
        disableActions();
        cancelInput();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
    }

    //private void OnApplicationFocus(bool focused)
    //{
    //    this.focused = focused;
    //    if (focused == false)
    //        cancelInput();
    //}

    private void cancelInput()
    {
        inputInfo = default;
    }

    private void enableActions()
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
    }

    private void disableActions()
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
    }
}
