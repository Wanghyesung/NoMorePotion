using System;
using Enzeun.SO;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 씬에 배치된 월드 오브젝트(꽃, 나무, 광물 등) 한 개의 런타임 상태입니다. 남은 횟수를 관리합니다.
    ///
    /// 횟수 차감 규칙 (기획)
    /// - 상호작용 타입(Interacting) : 상호작용 1회당 1 차감, 맞는 종류의 대미지가 들어오면 대미지 수치만큼 차감
    /// - 파괴 타입(Destroying) : 상호작용으로는 차감되지 않고, 맞는 종류의 대미지가 들어올 때만 대미지 수치만큼 차감
    ///   예) 식물 대미지 1/2/3 을 주는 소/중/대 포션은 횟수를 1/2/3 씩 차감합니다.
    /// - 횟수가 0 이 되면 소진되며, 소진 이벤트는 한 번만 발생합니다.
    ///
    /// SO(WorldObjectData)는 모든 오브젝트가 공유하는 설정값이라 남은 횟수 같은 변하는 값은 이 컴포넌트가 가집니다.
    /// </summary>
    public class WorldObject : MonoBehaviour
    {
        [SerializeField, LabelText("월드 오브젝트 데이터"), Required("월드 오브젝트 데이터를 연결해 주세요.")]
        private WorldObjectData _data;
        public WorldObjectData data => _data;

        [ShowInInspector, ReadOnly, LabelText("남은 횟수"), PropertyOrder(-2)]
        public int remainingCount { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("소진 여부"), PropertyOrder(-1)]
        public bool isDepleted { get; private set; }

        /// <summary>
        /// 남은 횟수가 바뀔 때 (이전, 이후) 값과 함께 발생합니다. 값이 바뀌지 않으면 발생하지 않습니다.
        /// </summary>
        public event Action<int, int> OnCountChanged;

        /// <summary>
        /// 횟수가 0 이 되어 소진되었을 때 한 번만 발생합니다.
        /// </summary>
        public event Action<WorldObject> OnDepleted;


        private void Awake()
        {
            ResetCount();
        }

        /// <summary>
        /// 남은 횟수를 SO 에 설정된 최대 횟수로 되돌리고 소진 상태를 다시 판정합니다.
        /// </summary>
        public void ResetCount()
        {
            if (_data == null)
            {
                Debug.LogWarning($"[WorldObject]: {name} 에 월드 오브젝트 데이터가 연결되지 않았습니다.");
                remainingCount = 0;
                isDepleted = true;
                return;
            }

            int previous = remainingCount;
            remainingCount = _data.maxCount;
            isDepleted = remainingCount <= 0;

            if (previous != remainingCount)
            {
                OnCountChanged?.Invoke(previous, remainingCount);
            }
        }

        /// <summary>
        /// 상호작용합니다. 상호작용 타입이면 횟수를 1 차감하고, 파괴 타입이면 아무것도 차감하지 않습니다.
        /// </summary>
        /// <returns>횟수가 차감되었으면 true</returns>
        public bool Interact()
        {
            if (_data == null || isDepleted)
            {
                return false;
            }

            if (_data.worldObjectDropType != WorldObjectDropType.Interacting)
            {
                return false;
            }

            return Decrease(1);
        }

        /// <summary>
        /// 포션의 월드 오브젝트 대미지 효과를 받습니다. 효과의 대상 종류에 이 오브젝트의 종류가 있을 때만 차감합니다.
        /// </summary>
        /// <returns>횟수가 차감되었으면 true</returns>
        public bool ReceivePotionDamage(WorldObjectDamagePotionEffect effect)
        {
            if (_data == null || effect == null || isDepleted)
            {
                return false;
            }

            if (!IsTarget(effect))
            {
                return false;
            }

            return Decrease(effect.damage);
        }

        private bool IsTarget(WorldObjectDamagePotionEffect effect)
        {
            for (int i = 0; i < effect.targetTypes.Count; i++)
            {
                if (effect.targetTypes[i] == _data.worldObjectType)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 남은 횟수를 amount 만큼 차감합니다. 0 아래로는 내려가지 않고, 0 이 되는 순간 소진 처리합니다.
        /// </summary>
        private bool Decrease(int amount)
        {
            if (amount <= 0 || isDepleted)
            {
                return false;
            }

            int previous = remainingCount;
            remainingCount = Mathf.Max(0, remainingCount - amount);

#if UNITY_EDITOR
            Debug.Log($"[WorldObject] : {name} 횟수 {previous} -> {remainingCount}");
#endif

            OnCountChanged?.Invoke(previous, remainingCount);

            if (remainingCount == 0)
            {
                isDepleted = true;
                OnDepleted?.Invoke(this);
            }

            return true;
        }

#if UNITY_EDITOR
        // ==================== 에디터 테스트용 ============================

        [Button("테스트: 상호작용", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void InteractForTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WorldObject]: Play 모드에서만 테스트할 수 있습니다.");
                return;
            }

            Interact();
        }

        [Button("테스트: 대상 종류 대미지", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void DamageForTest(int damage = 1)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[WorldObject]: Play 모드에서만 테스트할 수 있습니다.");
                return;
            }

            // 이 오브젝트의 종류를 대상으로 하는 대미지 효과를 만들어 전달합니다.
            var effect = new WorldObjectDamagePotionEffect();
            typeof(WorldObjectDamagePotionEffect).GetField("_targetTypes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(effect, new System.Collections.Generic.List<WorldObjectType> { _data.worldObjectType });
            typeof(WorldObjectDamagePotionEffect).GetField("_damage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(effect, damage);

            ReceivePotionDamage(effect);
        }

        [Button("테스트: 횟수 초기화", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void ResetForTest()
        {
            ResetCount();
        }
#endif
    }
}
