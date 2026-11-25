using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class MinimapUI : MonoBehaviour
{
    [Header("UI 참조")]
    [SF] private RawImage mapImage;
    [SF] private RectTransform mapRotator;
    [SF] private RectTransform playerArrow;

    [Header("미니맵 정보 (Minimap_MainMap.png)")]
    [SF] private Vector2 worldMin = new Vector2(-650f, -650f);
    [SF] private float worldSize = 1300f;

    [Header("표시")]
    [Tooltip("원 지름 월드 거리(m)")][SF]
    private float viewDiameter = 150f;

    private Player player;


    private void LateUpdate()
    {
        if (player == null)
            return;

        UpdateMap();
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
}
