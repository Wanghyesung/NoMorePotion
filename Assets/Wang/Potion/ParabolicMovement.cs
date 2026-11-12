using UnityEngine;

/*///////////////////////////////////////////
               ParabolicMovement
기능 : 복사한 포물선으로 이동하고 공중 목표 통과 후 강화된 중력으로 낙하
 *///////////////////////////////////////////

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class ParabolicMovement : MonoBehaviour
{
    private ThrowData throwData;
    private float elapsedTime;
    private LayerMask collisionMask;
    private Rigidbody body;
    private bool checkCollisions;
    public event System.Action<Vector3> OnHitAction;
    public bool IsFlying { get; private set; }

    [SerializeField] private float speedMultiplier = 1f;
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 2f;
    [SerializeField, Min(0.001f)] private float collisionRadius = 0.06f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        // 충돌 판정은 SphereCast만 사용하여 다른 몸을 밀지 않음
        body.detectCollisions = false;
    }

    // socket: 포션을 붙일 손 위치. 보관 중에는 부모 Transform이 이동을 담당
    public void AttachTo(Transform socket)
    {
        IsFlying = false;
        enabled = false;
        body.interpolation = RigidbodyInterpolation.None;
        transform.SetParent(socket, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        body.position = transform.position;
        body.rotation = transform.rotation;
    }

    // data: 발사 순간 확정된 월드 궤적. 조준선의 좌표 리스트는 참조하지 않음
    public void Launch(ThrowData data, LayerMask mask, float elapsedSeconds = 0f, bool checkCollisions = true)
    {
        this.checkCollisions = checkCollisions;
        collisionMask = mask;
        throwData = data;
        elapsedTime = Mathf.Max(0f, elapsedSeconds) * speedMultiplier;
        if (data.StopAtTarget)
            elapsedTime = Mathf.Min(elapsedTime, data.FlightTime);
        body.position = GetFlightPosition(elapsedTime);
        body.rotation = transform.rotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        IsFlying = true;
    }

    private void FixedUpdate()
    {
        if (IsFlying == false)
            return;

        elapsedTime += Time.fixedDeltaTime * speedMultiplier;
        if (throwData.StopAtTarget == true)
            elapsedTime = Mathf.Min(elapsedTime, throwData.FlightTime);

        Vector3 nextPosition = GetFlightPosition(elapsedTime);
        Vector3 delta = nextPosition - body.position;
        float distance = delta.magnitude;

        if (checkCollisions && distance > 0.0001f && Physics.SphereCast(body.position, collisionRadius,
            delta / distance, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Collide) == true)
        {
            body.MovePosition(body.position + delta / distance * hit.distance);
            IsFlying = false;
            OnHitAction?.Invoke(hit.point);
            return;
        }

        body.MovePosition(nextPosition);
        if (throwData.StopAtTarget == true && elapsedTime >= throwData.FlightTime)
            IsFlying = false;
    }
    // time: 배속이 반영된 비행 경과 시간. 공중 목표 이후에도 위치와 속도를 이어감
    private Vector3 GetFlightPosition(float time)
    {
        if (throwData.StopAtTarget == true || time <= throwData.FlightTime)
            return throwData.GetPosition(time);

        //만약 차지 지점이 없다면 그대로 포물선 운동
        float fallTime = time - throwData.FlightTime;
        Vector3 endVelocity = throwData.Velocity + throwData.Gravity * throwData.FlightTime;
        return throwData.Target + endVelocity * fallTime
            + 0.5f * throwData.Gravity * fallGravityMultiplier * fallTime * fallTime;
    }
}
