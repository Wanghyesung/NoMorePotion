using R3;
using UnityEngine;

public class OutGameUI : MonoBehaviour
{
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject connectingPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;
    
    [SerializeField] private PopupUI popup;


    private void Start()
    {
        NetworkFlowManager manager = NetworkFlowManager.Instance;
        if(manager == null )
        {
            Debug.LogError("[OutGameUI] NetworkFlowManager 없음");
            return;
        }

        manager.State.Subscribe(ApplyState).AddTo( this );
        manager.Error.Subscribe(popup.Show).AddTo( this );
        
    }


    private void ApplyState(FlowState state)
    {
        titlePanel.SetActive(state == FlowState.Title || state == FlowState.Connecting);
        connectingPanel.SetActive(state == FlowState.Connecting);
        lobbyPanel.SetActive(state == FlowState.Lobby);
        roomPanel.SetActive(state == FlowState.Room);
    }
}
