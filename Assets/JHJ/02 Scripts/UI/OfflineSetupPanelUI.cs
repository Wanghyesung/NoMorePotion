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
        // TODO: 오프라인 연결
        startBtn.onClick.AddListener(() => Debug.Log("[OfflineSetupPanelUI] 오프라인 시작: 추후 연결"));

    }

}
