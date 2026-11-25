using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;
using System.Collections.Generic;

public class MinimapUI : MonoBehaviour
{
    [Header("UI 참조")]
    [SF] private RawImage mapImage;
    [SF] private RectTransform mapRotator;
    [SF] private RectTransform playerArrow;
    [SF] private RectTransform iconRoot;

    [Header("미니맵 정보 (Minimap_MainMap.png)")]
    [SF] private Vector2 worldMin = new Vector2(-650f, -650f);
    [SF] private float worldSize = 1300f;

    [Header("표시")]
    [Tooltip("원 지름 월드 거리(m)")][SF]
    private float viewDiameter = 150f;

    private Player player;
    private readonly Dictionary<MinimapIcon, RectTransform> iconViews = new Dictionary<MinimapIcon, RectTransform>();
    private readonly List<MinimapIcon> removeBuffer = new List<MinimapIcon>();  // 지울것들 모아두는 리스트


    private void LateUpdate()
    {
        if (player == null)
            return;

        UpdateMap();
        UpdateIcons();
    }

    public void SetPlayer(Player target)
    {
        player = target;
    }

    private void UpdateMap()
    {
        Vector3 pos = player.transform.position;

        Vector2 center = new Vector2((pos.x - worldMin.x) / worldSize, (pos.z - worldMin.y) / worldSize);
        float view = viewDiameter / worldSize;

        mapImage.uvRect = new Rect(center.x - view / 2f, center.y - view / 2f, view, view);

        float yaw = player.transform.eulerAngles.y;

        if(MinimapSettings.RotateWithPlayer)
        {
            // 보는 방향이 위, 지도가 돌고 화살표는 고정
            mapRotator.localEulerAngles = new Vector3(0f, 0f, yaw);
            playerArrow.localEulerAngles = Vector3.zero;
        }
        else
        {   // 지도는 고정 화살표가 보는 방향에 따라 돔
            mapRotator.localEulerAngles = Vector3.zero;
            playerArrow.localEulerAngles = new Vector3(0f, 0f, -yaw);
        }
        
    }

    /// <summary>
    /// 아이콘 생성
    /// </summary>
    private RectTransform CreateIconView(MinimapIcon icon)
    {
        GameObject gameobj = new GameObject($"Icon_{icon.name}", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)gameobj.transform;
        rect.SetParent(iconRoot, false);
        rect.sizeDelta = Vector2.one * icon.Size;

        Image image = gameobj.GetComponent<Image>();
        image.sprite = icon.Sprite;
        image.color = icon.Color;
        image.raycastTarget = false;

        return rect;
    }

    /// <summary>
    /// 사라진 아이콘 정리
    /// </summary>
    private void RemoveMissingIcons()
    {
        removeBuffer.Clear();

        foreach(MinimapIcon icon in iconViews.Keys)
        {
            if (icon == null || !icon.isActiveAndEnabled)
                removeBuffer.Add(icon);
        }

        foreach(MinimapIcon icon in removeBuffer)
        {
            Destroy(iconViews[icon].gameObject);
            iconViews.Remove(icon);
        }
    }

    private void UpdateIcons()
    {
        RemoveMissingIcons();

        Vector3 center = player.transform.position;
        float mapSize = mapRotator.rect.width;
        float pixelPerMeter = mapSize / viewDiameter;
        float radius = mapSize / 2f;

        Quaternion upright = Quaternion.Inverse(mapRotator.localRotation);

        foreach(MinimapIcon icon in MinimapIcon.All)
        {
            // 내 플레이어 제외
            if (icon.transform == player.transform)
                continue;

            if(!iconViews.TryGetValue(icon, out RectTransform view))
            {
                view = CreateIconView(icon);
                iconViews.Add(icon, view);
            }

            Vector3 offset = icon.transform.position - center;
            Vector2 pos = new Vector2(offset.x, offset.z) * pixelPerMeter;

            float limit = radius - icon.Size / 2f;
            bool outside = pos.magnitude > limit;

            if (outside && icon.ClampToEdge)
                pos = pos.normalized * limit;

            view.gameObject.SetActive(!outside || icon.ClampToEdge);
            view.anchoredPosition = pos;
            view.localRotation = upright;
        }
    }
}
