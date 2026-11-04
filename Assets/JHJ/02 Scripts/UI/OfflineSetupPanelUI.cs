using UnityEngine;
using UnityEngine.UI;

public class OfflineSetupPanelUI : MonoBehaviour
{
    [SerializeField] private Button startBtn;
    [SerializeField] private Button leaveBtn;

    private void Start()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;

        if (manager == null)
        {
            Debug.LogError("[OfflineSetupPanelUI] NetworkFlowManager 없음");
            return;
        }

        leaveBtn.onClick.AddListener(manager.RequestLeaveOffline);
        startBtn.onClick.AddListener(manager.RequestStartOffline);

    }

}
