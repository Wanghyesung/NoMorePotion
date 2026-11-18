using Enzeun.SO;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트를 자동 배치할 지역입니다. 트리거 박스 콜라이더가 지역 범위이고, 그 안에서 위치를 뽑습니다.
    /// 어느 테이블에서 몇 개를 뽑을지만 정하며, 실제 배치는 WorldObjectAutoSpawner 가 합니다.
    /// 박스는 위쪽이 지면보다 높아야 합니다. (박스 윗면에서 아래로 쏴서 지면을 찾음)
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class WorldObjectSpawnRegion : MonoBehaviour
    {
        #region Config
        [SerializeField, Required("배치 테이블을 연결해 주세요."), LabelText("배치 테이블")]
        private WorldObjectSpawnTable _table;

        [SerializeField, LabelText("배치할 개수"), MinValue(0)]
        private int _count = 10;

        public WorldObjectSpawnTable Table => _table;
        public int Count => _count;
        #endregion

        #region Box
        private BoxCollider _box;

        private BoxCollider Box
        {
            get
            {
                if (_box == null)
                {
                    _box = GetComponent<BoxCollider>();
                }

                return _box;
            }
        }

        // 새로 붙였을 때 플레이어를 막지 않도록 트리거로 둡니다.
        private void Reset()
        {
            Box.isTrigger = true;
        }

        /// <summary>
        /// 박스 윗면 위의 무작위 지점입니다. (회전과 크기를 반영) 여기서 아래로 쏴서 지면을 찾습니다.
        /// </summary>
        public Vector3 NextRayOrigin(System.Random rng)
        {
            Vector3 size = Box.size;
            Vector3 local = Box.center + new Vector3(
                ((float)rng.NextDouble() - 0.5f) * size.x,
                size.y * 0.5f,
                ((float)rng.NextDouble() - 0.5f) * size.z);

            return transform.TransformPoint(local);
        }

        /// <summary>
        /// 박스 윗면에서 아래로 쏠 거리입니다.
        /// </summary>
        public float RayDistance => Box.bounds.size.y;
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Box.center, Box.size);
        }
        #endregion
    }
}
