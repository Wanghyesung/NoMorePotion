using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class ThrowAim : MonoBehaviour
{
    [SerializeField] private Transform aimDirection;
    [SerializeField, Range(0f, 80f)] private float launchAngle = 30f;
    [SerializeField, Min(2)] private int pointCount = 40;
    [SerializeField, Min(0.01f)] private float timeStep = 0.05f;
    [SerializeField] private List<Vector3> positions = new List<Vector3>(40);

    private LineRenderer lineRenderer;
    private Transform origin;
    private float throwSpeed;

    public IReadOnlyList<Vector3> Positions => positions;
    public ThrowTrajectory Trajectory { get; private set; }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        positions.Capacity = Mathf.Max(positions.Capacity, pointCount);
        Hide();
    }

    public void Show(Transform throwOrigin, float speed)
    {
        origin = throwOrigin;
        throwSpeed = speed;
        enabled = true;
        lineRenderer.enabled = true;
        Draw();
    }

    public void Hide()
    {
        enabled = false;
        origin = null;
        positions.Clear();
        lineRenderer.positionCount = 0;
        lineRenderer.enabled = false;
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
        Vector3 direction = Quaternion.AngleAxis(-launchAngle, aimDirection.right)
            * aimDirection.forward;
        Trajectory = new ThrowTrajectory(origin.position, direction * throwSpeed, Physics.gravity);

        positions.Clear();
        for (int i = 0; i < pointCount; i++)
            positions.Add(Trajectory.GetPosition(i * timeStep));

        lineRenderer.positionCount = positions.Count;
        for (int i = 0; i < positions.Count; i++)
            lineRenderer.SetPosition(i, positions[i]);
    }

    private void OnDisable()
    {
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }
}
