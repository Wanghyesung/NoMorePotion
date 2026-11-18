using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트를 놓지 않을 구역입니다. (마을, 플레이어 시작 지점, 보스 구역, 길 등)
    /// 이 콜라이더 안이거나 가까운 곳은 자동 배치에서 제외됩니다. 콜라이더는 박스, 구, 캡슐 같은 단순한 모양을 쓰세요.
    /// 모든 클라이언트가 같은 결과를 내야 하므로, 움직이는 것이 아니라 씬에 고정된 오브젝트에 붙여야 합니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WorldObjectSpawnBlocker : MonoBehaviour
    {
        private Collider _collider;

        private Collider Collider
        {
            get
            {
                if (_collider == null)
                {
                    _collider = GetComponent<Collider>();
                }

                return _collider;
            }
        }

        // 새로 붙였을 때 플레이어를 막지 않도록 트리거로 둡니다.
        private void Reset()
        {
            Collider.isTrigger = true;
        }

        /// <summary>
        /// point 가 이 구역 안이거나 margin 안쪽으로 가까우면 true 입니다.
        /// </summary>
        public bool Blocks(Vector3 point, float margin)
        {
            if (!Collider.enabled)
            {
                return false;
            }

            Vector3 closest = Collider.ClosestPoint(point);
            return (closest - point).sqrMagnitude <= margin * margin;
        }

        private void OnDrawGizmos()
        {
            Collider collider = Collider;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);

            if (collider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
            }
        }
    }
}
