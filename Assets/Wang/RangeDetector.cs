using UnityEngine;

/*///////////////////////////////////////////
                 RangeDetector
기능 : 반경과 LayerMask에 해당하는 가장 가까운 대상을 탐지
       Get<T>로 대상 또는 부모의 컴포넌트를 반환
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class RangeDetector : MonoBehaviour
{
    #region DetectionSettings
    [SerializeField, Min(0f)] private float radius = 2f;
    [SerializeField] private LayerMask targetLayers;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
    #endregion

    private Collider[] hits = new Collider[32];

    public GameObject Get()
    {
        Transform target = Get<Transform>();
        return target != null ? target.gameObject : null;
    }

    // T: 콜라이더 또는 부모에서 찾을 컴포넌트. 일치하는 대상이 없으면 null 반환
    public T Get<T>() where T : Component
    {
        Vector3 center = transform.position;
        int count = Detect(center);
        T nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            if (hit.transform.IsChildOf(transform))
                continue;

            T component = hit.GetComponentInParent<T>();
            if (component == null || transform.IsChildOf(component.transform))
                continue;

            float distance = (hit.ClosestPoint(center) - center).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = component;
            }
        }

        // 이전 탐지 대상이 버퍼에 참조로 남지 않도록 사용 구간만 비움
        System.Array.Clear(hits, 0, count);
        return nearest;
    }

    private int Detect(Vector3 center)
    {
        int count;
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(center, radius, hits, targetLayers, triggerInteraction);
            if (count < hits.Length)
                return count;

            // 대상이 많은 경우에만 확장하고, 이후 검색부터 같은 버퍼를 재사용
            System.Array.Resize(ref hits, hits.Length * 2);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
