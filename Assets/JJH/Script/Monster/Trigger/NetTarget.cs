using Photon.Pun;
using UnityEngine;

using static JJH_Constants;

/// <summary>
/// 몬스터 타겟을 위한 네트워크 변환
/// </summary>
public static class NetTarget
{
    /// <summary>
    /// 타겟을 ViewID로 변환
    /// 플레이어는 PlayerRegistryMember의 View, 그 외(수정 구슬 등)는 자기 또는 부모의 PhotonView
    /// </summary>
    public static int ToViewId(GameObject target)
    {
        if (target == null) return NetNone;

        if (target.TryGetComponent(out PlayerRegistryMember player) && player.View != null)
            return player.View.ViewID;

        PhotonView view = target.GetComponentInParent<PhotonView>();
        return view != null ? view.ViewID : NetNone;
    }

    /// <summary>
    /// ViewID를 타겟으로 변환
    /// </summary>
    public static GameObject FromViewId(int viewId)
    {
        if (viewId == NetNone) return null;

        // 플레이어는 PhotonView가 루트에 있고 타겟은 태그 오브젝트라 등록부에서 찾음
        GameObject player = PlayerRegistry.FindByViewId(viewId);
        if (player != null) return player;

        PhotonView view = PhotonView.Find(viewId);
        return view != null ? view.gameObject : null;
    }
}
