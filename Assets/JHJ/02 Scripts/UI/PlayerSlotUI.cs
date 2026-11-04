using TMPro;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

public class PlayerSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject playerView;      // 플레이어 있을 때 묶음
    [SerializeField] private GameObject waitingView;     // 대기 표시 묶음
    [SerializeField] private GameObject closedView;      // X 표시 묶음
                     
    [SerializeField] private TMP_Text nicknameText;      // playerView 안
    [SerializeField] private GameObject masterIcon;        // playerView 안
    [SerializeField] private GameObject localIcon;       // playerView 안


    private void SetView(GameObject showView)
    {
        playerView.SetActive(showView == playerView);
        waitingView.SetActive(showView == waitingView);
        closedView.SetActive(showView == closedView);
    }


    public void ShowPlayer(PhotonPlayer player)
    {
        SetView(playerView);

        nicknameText.text = player.NickName;
        masterIcon.SetActive(player.IsMasterClient);
        localIcon.SetActive(player.IsLocal);
    }

    public void ShowWaiting()
    {
        SetView(waitingView);
    }

    public void ShowClosed()
    {
        SetView(closedView);
    }
}
