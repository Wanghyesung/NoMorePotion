using UnityEngine;

/*///////////////////////////////////////////
               ParabolicMovement
기능 : 확정된 포물선으로 이동하고 실제 포션 크기로 충돌을 검사
 *///////////////////////////////////////////

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class ParabolicMovement : MonoBehaviour
{
    private ThrowData throwData;
    private float elapsedTime;
    private LayerMask collisionMask;
    private Rigidbody body;
    private CapsuleCollider potionCollider;
    private Transform thrower;
    private RaycastHit[] castHits = new RaycastHit[16];
    private bool checkCollisions;
    public event System.Action<Vector3> OnHitAction;
    public bool IsFlying { get; private set; }

    [SerializeField] private float speedMultiplier = 1f;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        potionCollider = GetComponent<CapsuleCollider>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        // 충돌 판정은 Cast만 사용하여 다른 몸을 밀지 않음
        body.detectCollisions = false;
    }

    // socket: 포션을 붙일 손 위치. 보관 중에는 부모 Transform이 이동을 담당
    public void AttachTo(Transform socket)
    {
        // 손에서 분리된 뒤에도 던진 사람의 몸은 충돌 대상에서 제외한다.
        thrower = socket.GetComponentInParent<Player>().transform;
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
        // 원격 포션은 RPC가 도착하는 동안 지난 시간을 반영한 위치에서 시작한다.
        elapsedTime = Mathf.Max(0f, elapsedSeconds) * speedMultiplier;
        body.position = throwData.GetPosition(elapsedTime);
        body.rotation = transform.rotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        IsFlying = true;
    }

    private void FixedUpdate()
    {
        if (IsFlying == false)
            return;

        elapsedTime += Time.fixedDeltaTime * speedMultiplier;
        Vector3 nextPosition = throwData.GetPosition(elapsedTime);
        // 현재 위치부터 다음 위치까지 캡슐로 훑어, 빠르게 날아가도 중간 장애물을 검사한다.
        if (checkCollisions && Cast(body.position, nextPosition, collisionMask, out RaycastHit hit))
        {
            body.MovePosition(Vector3.MoveTowards(body.position, nextPosition, hit.distance));
            IsFlying = false;
            OnHitAction?.Invoke(hit.point);
            return;
        }

        body.MovePosition(nextPosition);
        // 중간 충돌이 없어도 지정한 바닥 목표점에 도착하면 폭발한다.
        if (elapsedTime >= throwData.FlightTime)
        {
            IsFlying = false;
            // 원격 포션은 여기서 멈추고, 폭발은 소유자의 RPC를 기다린다.
            if (checkCollisions)
                OnHitAction?.Invoke(throwData.Target);
        }
    }

    // 실제 포션 콜라이더를 from에서 to까지 이동시켰을 때의 첫 충돌을 찾는다.
    public bool Cast(Vector3 from, Vector3 to, LayerMask mask, out RaycastHit hit)
    {
        Vector3 delta = to - from;
        float distance = delta.magnitude;
        hit = default;
        if (distance <= 0.0001f)
            return false;

        Vector3 scale = transform.lossyScale;
        // 캡슐의 길이 방향은 0=X, 1=Y, 2=Z이며 나머지 두 축으로 반지름을 구한다.
        int axis = potionCollider.direction;
        float radius = potionCollider.radius * Mathf.Max(
            Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));
        // 양 끝 반구의 중심까지 거리. 높이가 지름 이하면 구 형태가 된다.
        float halfLength = Mathf.Max(0f, potionCollider.height * Mathf.Abs(scale[axis]) * 0.5f - radius);
        Vector3 direction = axis == 0 ? transform.right : axis == 1 ? transform.up : transform.forward;
        Vector3 center = from + transform.TransformVector(potionCollider.center);
        Vector3 offset = direction * halfLength;
        // 콜라이더의 중심·회전·크기를 적용한 캡슐로 이동 구간을 검사한다.
        int count;
        while (true)
        {
            count = Physics.CapsuleCastNonAlloc(center + offset, center - offset, radius,
                delta / distance, castHits, distance, mask, QueryTriggerInteraction.Collide);
            if (count < castHits.Length)
                break;
            System.Array.Resize(ref castHits, castHits.Length * 2);
        }

        // 결과 순서는 보장되지 않으므로, 자신을 제외한 가장 가까운 충돌을 고른다.
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = castHits[i];
            if (candidate.collider == potionCollider
                || (thrower != null && candidate.collider.transform.IsChildOf(thrower)))
                continue;
            if (found == false || candidate.distance < hit.distance)
            {
                hit = candidate;
                found = true;
            }
        }
        return found;
    }

}
