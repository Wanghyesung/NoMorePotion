using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 포션 폭발의 설정값입니다. 소/중/대 폭발 반경과 효과가 닿는 레이어를 정합니다.
    /// 반경 수치는 임시 값이니 인스펙터에서 자유롭게 수정합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "PotionExplosionConfig", menuName = "Scriptable Objects/PotionExplosionConfig")]
    public class PotionExplosionConfig : ScriptableObject
    {
        // Resources 하위 경로입니다. (확장자 제외)
        public static readonly string ResourcePath = "ScriptableObjects/Config/PotionExplosionConfig";

        private static PotionExplosionConfig _instance;

        /// <summary>
        /// 설정 에셋을 한 번 불러와 재사용합니다. 에셋이 없으면 null 입니다.
        /// </summary>
        public static PotionExplosionConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<PotionExplosionConfig>(ResourcePath);
                }

                return _instance;
            }
        }

        [SerializeField, LabelText("소 폭발 반경 (m)"), MinValue(0.1f), BoxGroup("폭발 반경")]
        private float _smallRadius = 3f;

        [SerializeField, LabelText("중 폭발 반경 (m)"), MinValue(0.1f), BoxGroup("폭발 반경")]
        private float _mediumRadius = 5f;

        [SerializeField, LabelText("대 폭발 반경 (m)"), MinValue(0.1f), BoxGroup("폭발 반경")]
        private float _largeRadius = 8f;

        // Player, Enemy, WorldObject 레이어를 선택합니다. (WorldObject 레이어를 만든 뒤 여기서 추가)
        [SerializeField, LabelText("효과가 닿는 레이어")]
        private LayerMask _targetLayers;
        public LayerMask TargetLayers => _targetLayers;

        /// <summary>
        /// 폭발 범위(소/중/대)에 해당하는 반경을 반환합니다. 지정되지 않은 범위는 소와 같은 반경으로 처리합니다.
        /// </summary>
        public float GetRadius(PotionExplosionRange range)
        {
            switch (range)
            {
                case PotionExplosionRange.Medium:
                    return _mediumRadius;
                case PotionExplosionRange.Large:
                    return _largeRadius;
                default:
                    return _smallRadius;
            }
        }
    }
}
