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
        //[SerializeField, LabelText("포션 데이터"), Required("포션 데이터를 연결해 주세요.")]
        private PotionData _data;
        public PotionData Data => _data;

        /// <summary>
        /// 이 포션이 가진 제작 인스턴스입니다. 뱃속에서 꺼낸 포션이면 함정 여부가 들어 있고,
        /// 지정하지 않으면(데이터만 연결한 경우) 정상 포션으로 취급합니다.
        /// </summary>
        public PotionInstance Instance { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("폭발 여부")]
        public bool HasExploded { get; private set; }

        //============= 혜성님 스크립트에 필요한 필드 ==================================
        [SerializeField]
        public PotionMover potionMover;

        //============= 혜성님 스크립트에 필요한 필드 ==================================


        /// <summary>
        /// 생성한 쪽에서 포션 데이터만 지정할 때 사용합니다. (정상 포션)
        /// </summary>
        public void Initialize(PotionData data)
        {
            _data = data;
            Instance = new PotionInstance(data);
            HasExploded = false;
        }

        /// <summary>
        /// 뱃속에서 꺼낸 제작 인스턴스를 지정할 때 사용합니다. 함정 포션이면 키워드 효과가 폭발에 더해집니다.
        /// </summary>
        public void Initialize(PotionInstance instance)
        {
            Instance = instance;
            _data = instance != null ? instance.Data : null;
            HasExploded = false;

            if (potionMover != null)
            {
                potionMover.InitializePotionMover();
            }
        }

        // private void OnCollisionEnter(Collision collision)
        // {
        //     Explode();
        // }

        // /// <summary>
        // /// 이 오브젝트의 현재 위치에서 폭발합니다. 한 번만 동작합니다. (위치를 직접 지정하려면 ExplodeAt)
        // /// </summary>
        // [Button("테스트: 폭발", ButtonSizes.Large)]
        // public void Explode()
        // {
        //     ExplodeAt(transform.position);
        // }

        /// <summary>
        /// 지정한 위치에서 폭발합니다. 한 번만 동작합니다. (Explode 는 현재 위치, 이쪽은 지정 위치)
        /// 던지기는 부딪힌 위치(Explode), 마시기는 마시는 플레이어의 위치로 호출합니다.
        /// 어느 쪽이든 이 포션이 가진 인스턴스를 쓰므로 함정 키워드 효과가 함께 적용됩니다.
        /// </summary>
        //     public void ExplodeAt(Vector3 position)
        //     {
        //         if (HasExploded)
        //         {
        //             return;
        //         }

        //         HasExploded = true;

        //         // 인스턴스가 없으면(인스펙터에 데이터만 연결한 경우) 정상 포션으로 폭발합니다.
        //         if (Instance != null)
        //         {
        //             PotionExplosion.Explode(Instance, position);
        //         }
        //         else
        //         {
        //             PotionExplosion.Explode(_data, position);
        //         }

        //         Destroy(gameObject);
        //     }
    }
}
