using System.Collections.Generic;
using UnityEngine;
using Enzeun.SO;

/*///////////////////////////////////////////
                  ThrowAim
기능 : 중앙 조준 또는 최대 거리 아래의 바닥을 목표로 포물선을 계산
 *///////////////////////////////////////////

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class ThrowAim : MonoBehaviour
{
    #region AimSettings
    [SerializeField] private Camera aimCamera;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0.01f)] private float maxAimDistance = 40f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 100f;
    #endregion

    #region ArcSettings
    [SerializeField, Min(0.01f)] private float minArcHeight = 0.3f;
    [SerializeField, Min(0.01f)] private float maxArcHeight = 3f;
    #endregion

    #region LineSettings
    [SerializeField, Min(2)] private int pointCount = 40;
    [SerializeField, Min(2)] private int fadePointCount = 20;
    [SerializeField] private List<Vector3> positions = new List<Vector3>(40);
    [SerializeField] private GameObject rangePrefab;
    #endregion

    #region Runtime
    private LineRenderer lineRenderer;
    private Transform origin;
    private ParabolicMovement movement;
    private LayerMask collisionMask;
    private bool hasLandingPoint;
    private Gradient lineGradient;
    private GradientColorKey[] lineColors;
    private readonly GradientAlphaKey[] fadeAlphas = new GradientAlphaKey[3];
    private LineRenderer rangeRenderer;
    private PoolManager rangePool;
    private Vector3 targetNormal;
    private Vector3 landingPosition;
    private float explosionRadius;

    public ThrowData ThrowData { get; private set; }
    public bool HasTarget { get; private set; }
    #endregion

    public void SetCamera(Camera camera) => aimCamera = camera;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineGradient = lineRenderer.colorGradient;
        lineColors = lineGradient.colorKeys;
        lineGradient.mode = GradientMode.Blend;
        positions.Capacity = Mathf.Max(positions.Capacity, pointCount);
        Hide();
    }

    // throwOrigin: 움직이는 손의 월드 출발 위치
    public void Show(Transform throwOrigin)
    {
        origin = throwOrigin;
        PotionMover potion = throwOrigin.GetComponent<PotionMover>();
        movement = throwOrigin.GetComponent<ParabolicMovement>();
        collisionMask = potion.CollisionMask;
        explosionRadius = PotionExplosionConfig.Instance.GetRadius(potion.Data.potionExplosionRange);
        enabled = true;
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

        Draw();
    }

    private void Draw()
    {
        if (TryCreateThrowData(origin.position, out ThrowData data) == false)
        {
            ClearLine();
            return;
        }

        HasTarget = true;
        ThrowData = data;
        BuildTrajectory(data);

        lineRenderer.enabled = true;
        lineRenderer.positionCount = positions.Count;
        for (int i = 0; i < positions.Count; i++)
            lineRenderer.SetPosition(i, positions[i]);

        UpdateLineFade();
        DrawLandingRange();
    }

    // 조준선 좌표를 만들면서 첫 충돌 위치를 찾는다.
    private void BuildTrajectory(ThrowData data)
    {
        // 바닥 목표점까지 그리되, 중간에 먼저 맞는 물체가 있으면 거기서 선을 끝낸다.
        hasLandingPoint = true;
        landingPosition = data.Target;
        positions.Clear();
        positions.Add(data.Origin);
        float step = data.FlightTime / (pointCount - 1);

        // shortcut: 선의 구간으로 착지를 근사한다. 정밀도가 더 필요하면 pointCount를 늘린다.
        for (int i = 1; i < pointCount; i++)
        {
            Vector3 previous = positions[positions.Count - 1];
            Vector3 next = data.GetPosition(i * step);
            if (movement.Cast(previous, next, collisionMask, out RaycastHit hit))
            {
                positions.Add(Vector3.MoveTowards(previous, next, hit.distance));
                landingPosition = hit.point;
                targetNormal = hit.normal;
                // 조준 대상 레이어의 바닥에 닿았을 때만 범위 원을 표시한다.
                hasLandingPoint = hit.collider.isTrigger == false
                    && (targetLayers.value & (1 << hit.collider.gameObject.layer)) != 0
                    && Vector3.Dot(targetNormal, Vector3.up) > 0.1f;
                return;
            }

            positions.Add(next);
        }
    }

    // 예상 착지면의 위치와 기울기에 맞춰 폭발 범위 원을 배치한다.
    private void DrawLandingRange()
    {
        if (hasLandingPoint == false)
        {
            if (rangeRenderer != null)
                rangeRenderer.gameObject.SetActive(false);
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, targetNormal);
        Vector3 center = landingPosition + targetNormal * 0.03f;
        if (rangeRenderer == null)
        {
            rangePool = PoolManager.Instance;
            rangeRenderer = rangePool.Get(rangePrefab, center, rotation).GetComponent<LineRenderer>();
        }
        rangeRenderer.transform.SetPositionAndRotation(center, rotation);
        rangeRenderer.transform.localScale = Vector3.one * explosionRadius;
        rangeRenderer.gameObject.SetActive(true);
        rangeRenderer.enabled = true;
    }

    // 바닥을 못 찍으면 최대 조준 거리 지점에서 수직 아래의 바닥을 찾는다.
    public bool TryCreateThrowData(Vector3 throwOrigin, out ThrowData data)
    {
        data = default;
        if (aimCamera == null)
            return false;

        // 화면 중앙으로 먼저 쏜다. 표면의 위쪽 성분으로 벽·천장을 바닥과 구분한다.
        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool grounded = Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, targetLayers,
            QueryTriggerInteraction.Ignore) && Vector3.Dot(hit.normal, Vector3.up) > 0.1f;

        
        if (grounded == false)
        {
            // 조준 Ray의 최대 거리 끝점에서 수직 아래로 탐색한다. 여기에도 바닥이 없으면 투척하지 않는다.
            if (Physics.Raycast(ray.GetPoint(maxAimDistance), Vector3.down, out hit,
                groundProbeDistance, targetLayers, QueryTriggerInteraction.Ignore) == false
                || Vector3.Dot(hit.normal, Vector3.up) <= 0.1f)
                return false;
        }

        targetNormal = hit.normal;
        // 가까운 목표는 낮게, 먼 목표는 높게 던지도록 수평 거리를 포물선 높이로 바꾼다.
        float distance = Vector3.ProjectOnPlane(hit.point - throwOrigin, Physics.gravity).magnitude;
        float ratio = Mathf.InverseLerp(0f, maxAimDistance, distance);
        float arcHeight = Mathf.Lerp(minArcHeight, maxArcHeight, ratio);
        data = new ThrowData(throwOrigin, hit.point, Physics.gravity, arcHeight);
        return true;
    }

    private void UpdateLineFade()
    {
        int fadeStart = Mathf.Max(0, positions.Count - fadePointCount);
        float totalLength = 0f;
        float startLength = 0f;
        // Gradient는 선 길이 기준이므로 마지막 포인트 구간의 실제 길이 비율을 구함
        for (int i = 1; i < positions.Count; i++)
        {
            totalLength += Vector3.Distance(positions[i - 1], positions[i]);
            if (i == fadeStart)
                startLength = totalLength;
        }

        //선의 어느 위치에서 얼마나 투명하게 보일지
        float start = totalLength > 0f ? startLength / totalLength : 0f;
        fadeAlphas[0] = new GradientAlphaKey(1f, 0f);
        fadeAlphas[1] = new GradientAlphaKey(1f, start); // 흐려지기 시작할 위치까지: 선명하게 유지
        fadeAlphas[2] = new GradientAlphaKey(0f, 1f);
        lineGradient.SetKeys(lineColors, fadeAlphas);
        lineRenderer.colorGradient = lineGradient;
    }

    private void ClearLine()
    {
        HasTarget = false;
        hasLandingPoint = false;
        positions.Clear();
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
        ReleaseRange();
    }

    private void ReleaseRange()
    {
        if (rangeRenderer == null)
            return;

        rangeRenderer.enabled = false;
        // 씬 종료 시 풀부터 파괴된 경우에는 씬의 오브젝트 정리에 맡깁니다.
        if (rangePool != null)
            rangePool.Release(rangeRenderer.gameObject);
        rangeRenderer = null;
        rangePool = null;
    }

    private void OnDisable()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;
        ReleaseRange();
    }
}
