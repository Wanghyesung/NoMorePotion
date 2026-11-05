using Enzeun.SO;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 던져진 포션 오브젝트입니다. 무언가에 부딪히면 그 위치에서 폭발하고 사라집니다.
    /// 폭발과 효과 적용은 PotionExplosion 이 맡습니다. (마시기는 이 컴포넌트 없이 PotionExplosion 을 직접 호출)
    /// </summary>
    public class Potion : MonoBehaviour
    {
        [SerializeField, LabelText("포션 데이터"), Required("포션 데이터를 연결해 주세요.")]
        private PotionData _data;
        public PotionData Data => _data;

        [ShowInInspector, ReadOnly, LabelText("폭발 여부")]
        public bool HasExploded { get; private set; }

        /// <summary>
        /// 생성한 쪽에서 포션 데이터를 지정할 때 사용합니다.
        /// </summary>
        public void Initialize(PotionData data)
        {
            _data = data;
            HasExploded = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Explode();
        }

        /// <summary>
        /// 현재 위치에서 폭발합니다. 한 번만 동작합니다.
        /// </summary>
        [Button("테스트: 폭발", ButtonSizes.Large)]
        public void Explode()
        {
            if (HasExploded)
            {
                return;
            }

            HasExploded = true;
            PotionExplosion.Explode(_data, transform.position);
            Destroy(gameObject);
        }
    }
}
