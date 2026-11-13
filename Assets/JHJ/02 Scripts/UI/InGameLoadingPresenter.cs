using ExitGames.Client.Photon;
using Photon.Pun;
using R3;
using System.Linq;
using UnityEngine;

public class InGameLoadingPresenter : MonoBehaviourPunCallbacks
{
    [SerializeField] private LoadingPanelUI panel;

    private void Start()
    {
        Refresh();

        if (GameSessionManager.Instance == null)
        {
            Debug.LogWarning("[InGameLoadingPresenter] GameSessionManager 없음, 로딩 패널이 꺼지지 않음");
            return;
        }

        GameSessionManager.Instance.Phase
            .Where(p => p == SessionPhase.Playing)
            .Take(1)
            .Subscribe(_ => panel.CompleteAndHide())
            .AddTo(this);
    }

    public override void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, Hashtable changedProps)
    {
        Refresh();
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Refresh();
    }

    private void Refresh()
    {
        int loaded = PhotonNetwork.PlayerList.Count(PlayerProps.IsLoaded);
        panel.SetProgress(loaded, PhotonNetwork.PlayerList.Length);
    }
}
