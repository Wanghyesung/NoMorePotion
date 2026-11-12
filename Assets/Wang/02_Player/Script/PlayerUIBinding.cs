using Photon.Pun;
using UnityEngine;

// 생성된 내 플레이어를 씬 HUD와 카메라에 연결한다.
[DisallowMultipleComponent]
public sealed class PlayerUIBinding : MonoBehaviour
{
    private Player player;
    [SerializeField] private PlayerInfoUI playerInfoUI;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private VisionEffect visionEffect;

    public void SetPlayer(Player target)
    {
        if (target == null)
            return;

        PhotonView view = target.GetComponent<PhotonView>();
        if (view != null && view.IsMine == false)
            return;

        if (playerInfoUI != null && player != target)
            playerInfoUI.UnbindPlayer(player);

        if (player != null && player != target)
            player.GetComponent<PlayerStatusEffects>().SetVisionEffect(null);

        player = target;
        if(visionEffect != null)
            player.GetComponent<PlayerStatusEffects>().SetVisionEffect(visionEffect);
            
        // 생성 시 한 번만 찾아, 비활성 조준 오브젝트에도 씬 카메라를 전달한다.
        foreach (ThrowAim aim in player.GetComponentsInChildren<ThrowAim>(true))
            aim.SetCamera(aimCamera);

        if (playerInfoUI != null)
            playerInfoUI.BindPlayer(player);
        if (cameraManager != null)
            cameraManager.SetPlayer(player);
    }

    private void OnDestroy()
    {
        if (player != null)
            player.GetComponent<PlayerStatusEffects>().SetVisionEffect(null);
        if (playerInfoUI != null)
            playerInfoUI.UnbindPlayer(player);
    }
}
