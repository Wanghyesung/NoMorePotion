using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class OfflineSetupPanelUI : MonoBehaviour
{
    [SF] private Button newGame;
    [SF] private Button continueBtn;
    [SF] private Button leaveBtn;
    [SF] private SaveSlotUI[] slots;
    [SF] private ConfirmPopupUI confirmPopup;

    private NetworkFlowManager manager;
    private int selected = -1;

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].Setup(i, OnSlotChanged);
    }

    private void Start()
    {
        manager = NetworkFlowManager.Instance;

        if (manager == null)
        {
            Debug.LogError("[OfflineSetupPanelUI] NetworkFlowManager 없음");
            return;
        }

        continueBtn.onClick.AddListener(OnContinue);
        newGame.onClick.AddListener(() => OnNewGame().Forget());
        leaveBtn.onClick.AddListener(manager.RequestLeaveOffline);
    }

    private void OnEnable()
    {
        selected = -1;
        foreach (SaveSlotUI slot in slots)
        {
            slot.Refresh();
            slot.SetSelected(false);
        }
        UpdateButtons();
    }


    private void OnSlotChanged()
    {
        selected = -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsSelected)
            {
                selected = i;
                break;
            }
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool hasSelection = selected >= 0;
        continueBtn.interactable = hasSelection && slots[selected].HasData;
        newGame.interactable = hasSelection;
    }

    private void OnContinue()
    {
        if (selected < 0 || !slots[selected].HasData)
            return;

        SaveSession.Begin(selected, slots[selected].Data);
        manager.RequestStartOffline();
    }

    private async UniTaskVoid OnNewGame()
    {
        if (selected < 0)
            return;

        if (slots[selected].HasData)
        {
            bool ok = await confirmPopup.ShowAsync("기존 저장이 사라집니다.\n새로 시작할까요?");
            if (!ok)
                return;
        }

        SaveSession.Begin(selected, null);
        manager.RequestStartOffline();
    }
}
