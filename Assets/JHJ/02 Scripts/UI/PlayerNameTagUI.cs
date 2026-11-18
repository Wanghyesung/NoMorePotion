using Photon.Pun;
using TMPro;
using UnityEngine;
using SF = UnityEngine.SerializeField;
using PhotonPlayer = Photon.Realtime.Player;

public class PlayerNameTagUI : MonoBehaviourPunCallbacks
{
    [SF] private Canvas canvas;
    [SF] private CanvasGroup group;
    [SF] private TMP_Text nameText;
    [SF] private bool showOnLocalPlayer = false;
    [SF] private float maxDistance = 40;
    [SF] private float fadeRange = 5;

    private PhotonView view;
    private Camera cam;
    private bool active;

    private void Awake()
    {
        view = GetComponentInParent<PhotonView>();
    }

    public override void OnEnable()
    {
        base.OnEnable();
        Refresh();
    }

    private void LateUpdate()
    {
        if (!active)
            return;

        if(cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
            canvas.worldCamera = cam;
        }

        canvas.transform.rotation = cam.transform.rotation;

        float dist = Vector3.Distance(cam.transform.position, canvas.transform.position);
        float t = Mathf.InverseLerp(maxDistance - fadeRange, maxDistance, dist);

        group.alpha = 1 - t;
    }



    private void Refresh()
    {
        PhotonPlayer owner = view.Owner;
        active = owner != null && (!view.IsMine || showOnLocalPlayer);
        canvas.enabled = active;

        if (!active)
            return;

        nameText.text = string.IsNullOrEmpty(owner.NickName) ? $"Player {owner.ActorNumber}" : owner.NickName;
        int colorIndex = PlayerProps.GetColorIndex(owner);

        if (colorIndex != PlayerProps.NO_COLOR)
            nameText.color = PlayerColors.Get(colorIndex);
    }

}
