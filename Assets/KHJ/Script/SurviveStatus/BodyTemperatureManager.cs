using UnityEngine;
using System;
using Sirenix.OdinInspector;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 체온 상태를 명시하는 Enum
    /// </summary>
    public enum BodyTemperatureState
    {
        Normal,
        Cold,
        Hot
    }

    /// <summary>
    /// 체온 상태를 관리하는 클래스입니다. 체온이 높거나 낮을 때 이벤트를 발생시킵니다.
    /// </summary>
    public class BodyTemperatureManager : MonoBehaviour
    {
        // 기본값 : 50%
        // 구간 판정 : 70% 이상 -> 더움 상태 / 30% 이하 -> 추움 상태

        // TODO(미구현 기획): 용암지대 : 체온 상승 / 빙하지대 : 체온 감소


        // 체온 초기값
        [SerializeField, LabelText("체온 초기값")]
        private int initialBodyTemperature = 50;

        // 더움 임계값
        [SerializeField, LabelText("더움 임계값")]
        private int hotThreshold = 70;

        // 추움 임계값
        [SerializeField, LabelText("추움 임계값")]
        private int coldThreshold = 30;

        // 현재 체온 상태
        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        public BodyTemperatureState currentBodyTemperatureState { get; private set; } = BodyTemperatureState.Normal;

        /// <summary>
        /// 체온 상태가 변경됨을 알려주는 이벤트. 값 : Prev,Next
        /// </summary>
        public event Action<BodyTemperatureState, BodyTemperatureState> OnBodyTemperatureStateChanged;


        private int _bodyTemperature;

        /// <summary>
        /// 현재 체온 수치(0~100). 변경은 증감 메서드로만 합니다.
        /// </summary>
        [ShowInInspector, ReadOnly, PropertyOrder(-2)]
        public int bodyTemperature
        {
            get => _bodyTemperature;
            private set
            {
                _bodyTemperature = Mathf.Clamp(value, 0, 100);
            }
        }

        /// <summary>
        /// 값을 초기값으로 되돌리고 구간을 다시 판정합니다. 구간이 같으면 이벤트는 발생하지 않습니다.
        /// </summary>
        public void ResetBodyTemperature()
        {
            _bodyTemperature = initialBodyTemperature;

#if UNITY_EDITOR
            Debug.Log($"[BodyTemperatureManager] : 플레이어의 체온을 초기값 {initialBodyTemperature}으로 재설정 합니다.");
#endif
            CheckCurrentBodyTemp();
        }

        /// <summary>
        /// amount(양수)만큼 증가 후 0~100 으로 제한하고 구간을 다시 판정합니다.
        /// </summary>
        [Button, LabelText("체온 증가")]
        public void IncreaseBodyTemperature(int amount)
        {
            bodyTemperature += amount;
#if UNITY_EDITOR
            Debug.Log($"[BodyTemperatureManager] : 플레이어의 체온을 {amount}만큼 증가시킵니다. 현재 체온: {bodyTemperature}");
#endif

            CheckCurrentBodyTemp();
        }

        /// <summary>
        /// amount(양수)만큼 감소 후 0~100 으로 제한하고 구간을 다시 판정합니다.
        /// </summary>
        [Button, LabelText("체온 감소")]
        public void DecreaseBodyTemperature(int amount)
        {
            bodyTemperature -= amount;
#if UNITY_EDITOR
            Debug.Log($"[BodyTemperatureManager] : 플레이어의 체온을 {amount}만큼 감소시킵니다. 현재 체온: {bodyTemperature}");
#endif

            CheckCurrentBodyTemp();
        }

        /// <summary>
        /// 현재 수치로 구간을 판정합니다. 구간이 바뀐 경우에만 이벤트가 발생합니다.
        /// </summary>
        private void CheckCurrentBodyTemp()
        {
            BodyTemperatureState prevState = currentBodyTemperatureState;
            if (bodyTemperature >= hotThreshold)
            {
                currentBodyTemperatureState = BodyTemperatureState.Hot;
            }
            else if (bodyTemperature <= coldThreshold)
            {
                currentBodyTemperatureState = BodyTemperatureState.Cold;
            }
            else
                currentBodyTemperatureState = BodyTemperatureState.Normal;

            if (prevState != currentBodyTemperatureState)
            {
                OnBodyTemperatureStateChanged?.Invoke(prevState, currentBodyTemperatureState);
#if UNITY_EDITOR
                Debug.Log($"[BodyTemperatureManager] : 체온 상태가 '{currentBodyTemperatureState}'로 변경되었습니다. prev: {prevState}.");
#endif
            }
        }
    }
}
