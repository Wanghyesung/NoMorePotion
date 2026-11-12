using System.Collections.Generic;
using UnityEngine;
using Enzeun.SO;

/*///////////////////////////////////////////
                  ThrowAim
기능 : 카메라 화면 중앙으로 목표 지점을 선택하고 도착 궤적을 역산
       거리에 따라 포물선 높이를 조절하고 List 좌표를 LineRenderer에 표시
 *///////////////////////////////////////////

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class ThrowAim : MonoBehaviour
{
    #region AimSettings
    [SerializeField] private Camera aimCamera;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField, Min(0.01f)] private float maxAimDistance = 100f;
    [SerializeField, Min(0.01f)] private float fallbackDistance = 10f;
    #endregion

    #region ArcSettings
    [SerializeField, Min(0.01f)] private float minArcHeight = 0.3f;
    [SerializeField, Min(0.01f)] private float maxArcHeight = 3f;
    [SerializeField, Min(0.01f)] private float maxArcDistance = 15f;
    #endregion

    #region LineSettings
    [SerializeField, Min(2)] private int pointCount = 40;
    [SerializeField, Min(2)] private int fadePointCount = 20;
    [SerializeField] private List<Vector3> positions = new List<Vector3>(40);
    public IReadOnlyList<Vector3> Positions => positions;
    [SerializeField] private GameObject rangePrefab;
    #endregion

    #region Runtime
    private LineRenderer lineRenderer;
    private Transform origin;
    private Gradient lineGradient;
    private GradientColorKey[] lineColors;
    private readonly GradientAlphaKey[] fadeAlphas = new GradientAlphaKey[3];
    private LineRenderer rangeRenderer;
    private PoolManager rangePool;
    private Vector3 targetNormal;
    private float explosionRadius;

    public ThrowData ThrowData { get; private set; }
    public Vector3 TargetPosition { get; private set; }
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
        TargetPosition = data.Target;

        positions.Clear();
        float timeStep = ThrowData.FlightTime / (pointCount - 1);
        for (int i = 0; i < pointCount; i++)
            positions.Add(ThrowData.GetPosition(i * timeStep));

        lineRenderer.enabled = true;
        lineRenderer.positionCount = positions.Count;
        for (int i = 0; i < positions.Count; i++)
            lineRenderer.SetPosition(i, positions[i]);

        UpdateLineFade();
        DrawLandingRange();
    }

    private void DrawLandingRange()
    {
        // 벽·천장과 공중 목표에서는 지면 범위를 표시하지 않습니다.
        bool grounded = ThrowData.StopAtTarget && Vector3.Dot(targetNormal, Vector3.up) > 0.1f;
        if (grounded == false)
        {
            if (rangeRenderer != null)
                rangeRenderer.gameObject.SetActive(false);
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, targetNormal);
        Vector3 center = TargetPosition + targetNormal * 0.03f;
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

    // 커서 위치와 무관하게 실제 카메라 뷰포트 중앙을 조준한다.
    public bool TryCreateThrowData(Vector3 throwOrigin, out ThrowData data)
    {
        data = default;
        targetNormal = Vector3.zero;
        if (aimCamera == null)
            return false;

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hitSurface = Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, targetLayers,
            QueryTriggerInteraction.Ignore);
        Vector3 target = hitSurface == true ? hit.point : ray.GetPoint(fallbackDistance);
        targetNormal = hitSurface ? hit.normal : Vector3.zero;

        // 수평 거리 0~maxArcDistance를 높이 minArcHeight~maxArcHeight로 변환
        float distance = Vector3.ProjectOnPlane(target - throwOrigin, Physics.gravity).magnitude;
        float ratio = Mathf.InverseLerp(0f, maxArcDistance, distance);
        float arcHeight = Mathf.Lerp(minArcHeight, maxArcHeight, ratio);

        // ThrowData(월드 출발 위치, 월드 목표 위치, 중력 가속도, 정점 높이)
        data = new ThrowData(throwOrigin, target, Physics.gravity, arcHeight, hitSurface);
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
