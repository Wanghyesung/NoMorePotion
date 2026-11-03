using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/*///////////////////////////////////////////
                  ThrowAim
기능 : 마우스 화면 좌표로 목표 지점을 선택하고 도착 궤적을 역산
       거리에 따라 포물선 높이를 조절하고 List 좌표를 LineRenderer에 표시
 *///////////////////////////////////////////

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class ThrowAim : MonoBehaviour
{
    #region AimSettings
    [SerializeField] private Camera aimCamera;
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0.01f)] private float maxAimDistance = 100f;
    #endregion

    #region ArcSettings
    [SerializeField, Min(0.01f)] private float minArcHeight = 0.3f;
    [SerializeField, Min(0.01f)] private float maxArcHeight = 3f;
    [SerializeField, Min(0.01f)] private float maxArcDistance = 15f;
    #endregion

    #region LineSettings
    [SerializeField, Min(2)] private int pointCount = 40;
    [SerializeField] private List<Vector3> positions = new List<Vector3>(40);
    public IReadOnlyList<Vector3> Positions => positions;
    #endregion

    #region Runtime
    private LineRenderer lineRenderer;
    private Transform origin;

    public ThrowData ThrowData { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public bool HasTarget { get; private set; }
    #endregion

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        positions.Capacity = Mathf.Max(positions.Capacity, pointCount);
        Hide();
    }

    // throwOrigin: 움직이는 손의 월드 출발 위치
    public void Show(Transform throwOrigin)
    {
        origin = throwOrigin;
        enabled = true;
        if (cameraManager != null)
            cameraManager.SetZoom(true);
        LateUpdate();
    }

    public void Hide()
    {
        enabled = false;
        origin = null;
        ClearLine();
    }

    private void LateUpdate()
    {
        if (origin == null)
        {
            Hide();
            return;
        }

        Draw(Mouse.current.position.ReadValue());
    }

    // screenPosition: 화면의 마우스 X·Y 좌표. 카메라 Ray로 월드 목표 위치를 구함
    private void Draw(Vector2 screenPosition)
    {
        Ray ray = aimCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, targetLayers,
                QueryTriggerInteraction.Ignore))
        {
            ClearLine();
            return;
        }

        HasTarget = true;
        TargetPosition = hit.point;
        // 수평 거리 0~maxArcDistance를 높이 minArcHeight~maxArcHeight로 변환
        float distance = Vector3.ProjectOnPlane(TargetPosition - origin.position, Physics.gravity).magnitude;
        float ratio = Mathf.InverseLerp(0f, maxArcDistance, distance);
        float arcHeight = Mathf.Lerp(minArcHeight, maxArcHeight, ratio);

        // ThrowData(월드 출발 위치, 월드 목표 위치, 중력 가속도, 정점 높이)
        ThrowData = new ThrowData(origin.position, TargetPosition, Physics.gravity, arcHeight);

        positions.Clear();
        // 비행 시간 전체를 나누므로 마지막 점은 목표 위치에 도달
        float timeStep = ThrowData.FlightTime / (pointCount - 1);
        for (int i = 0; i < pointCount; i++)
            positions.Add(ThrowData.GetPosition(i * timeStep));

        lineRenderer.enabled = true;
        lineRenderer.positionCount = positions.Count;
        for (int i = 0; i < positions.Count; i++)
            lineRenderer.SetPosition(i, positions[i]);
    }

    private void ClearLine()
    {
        HasTarget = false;
        positions.Clear();
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
    }

    private void OnDisable()
    {
        if (cameraManager != null)
            cameraManager.SetZoom(false);
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }
}
