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
        // 기본값 : 50% / 용암지대 : 체온 상승 / 빙하지대 : 체온 감소
        // 70% 이상 -> 더움상태
        // 30% 이하 -> 추움상태


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
        public BodyTemperatureState currentBodyTemperatureState { get; private set; } = BodyTemperatureState.Normal;

        /// <summary>
        /// 체온 상태가 변경됨을 알려주는 이벤트. 값 : Prev,Next
        /// </summary>
        public event Action<BodyTemperatureState, BodyTemperatureState> OnBodyTemperatureStateChanged;


        private int _bodyTemperature;
        public int bodyTemperature
        {
            get => _bodyTemperature;
            private set
            {
                _bodyTemperature = Mathf.Clamp(value, 0, 100);
            }
        }

        public void ResetBodyTemperature()
        {
            _bodyTemperature = initialBodyTemperature;

#if UNITY_EDITOR
            Debug.Log($"[BodyTemperatureManager] : 플레이어의 체온을 초기값 {initialBodyTemperature}으로 재설정 합니다.");
#endif
            CheckCurrentBodyTemp();
        }

        public void IncreaseBodyTemperature(int amount)
        {
            bodyTemperature += amount;
#if UNITY_EDITOR
            Debug.Log($"[BodyTemperatureManager] : 플레이어의 체온을 {amount}만큼 증가시킵니다. 현재 체온: {bodyTemperature}");
#endif

            CheckCurrentBodyTemp();
        }

        public void DecreaseBodyTemperature(int amount)
        {
            bodyTemperature -= amount;
#if UNITY_EDITOR
            Debug.Log($"[BodyTemperatureManager] : 플레이어의 체온을 {amount}만큼 감소시킵니다. 현재 체온: {bodyTemperature}");
#endif

            CheckCurrentBodyTemp();
        }

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
