using Photon.Pun;
using UnityEngine;

using SF = UnityEngine.SerializeField;

/// <summary>
/// 플레이어 프리팹의 Player 태그가 붙은 콜라이더 오브젝트에 부착
/// (몬스터 ChaseTrigger가 타겟으로 기록하는 오브젝트와 같아야 함)
/// </summary>
public class PlayerRegistryMember : MonoBehaviour
{
    [SF] private PhotonView view;
    public PhotonView View => view;

    private void Awake()
    {
        if (view == null)
            view = GetComponentInParent<PhotonView>();   // 플레이어 루트의 PhotonView
    }

    private void OnEnable() => PlayerRegistry.Register(this);
    private void OnDisable() => PlayerRegistry.Unregister(this);
}
