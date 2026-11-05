using System;
using Sirenix.OdinInspector;
using UnityEngine;



namespace Enzeun.Runtime
{
    /// <summary>
    /// 수분 상태를 명시하는 Enum. 구간이 서로 대체되는 관계입니다.
    /// </summary>
    public enum HydrationState
    {
        Normal,
        Moist,
        Thirsty,
        Dehydrated,
    }

    /// <summary>
    /// 수분 상태를 관리하는 클래스입니다. 수분 구간이 바뀔 때 이벤트를 발생시킵니다.
    /// </summary>
    public class HydrationManager : MonoBehaviour
    {
        // 기본값 : 50%

        // 5초당 1% 감소 (러프)
        // 10% 이하 -> 갈증 디버프
        // 0% -> 갈증 에서 탈수 디버프로 변경

        // 70% 이상 -> 촉촉함 버프


        // 수분 초기값
        [SerializeField, LabelText("수분 초기값")]
        private int initialHydration = 50;

        // 촉촉함 버프 임계값
        [SerializeField, LabelText("촉촉함 버프 임계값")]
        private int moistThreshold = 70;

        // 갈증 디버프 임계값
        [SerializeField, LabelText("갈증 디버프 임계값")]
        private int thirstyThreshold = 10;

        // 탈수 디버프 임계값
        [SerializeField, LabelText("탈수 디버프 임계값")]
        private int dehydratedThreshold = 0;

        // 현재 수분 상태
        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        public HydrationState currentHydrationState { get; private set; } = HydrationState.Normal;

        /// <summary>
        /// 수분 상태가 변경됨을 알려주는 이벤트. 값 : Prev,Next
        /// </summary>
        public event Action<HydrationState, HydrationState> OnHydrationStateChanged;

        private int _hydration;
        [ShowInInspector, ReadOnly, PropertyOrder(-2)]
        public int hydration
        {
            get => _hydration;
            private set
            {
                _hydration = Mathf.Clamp(value, 0, 100);
            }
        }

        public void ResetHydration()
        {
            _hydration = initialHydration;
#if UNITY_EDITOR
            Debug.Log($"[HydrationManager] : 플레이어의 수분을 초기값 {initialHydration}으로 재설정 합니다.");
#endif
            CheckCurrentHydration();
        }

        [Button, LabelText("수분 증가")]
        public void IncreaseHydration(int amount)
        {
            hydration += amount;
#if UNITY_EDITOR
            Debug.Log($"[HydrationManager] : 플레이어의 수분을 {amount}만큼 증가시킵니다. 현재 수분: {hydration}");
#endif

            CheckCurrentHydration();
        }

        [Button, LabelText("수분 감소")]
        public void DecreaseHydration(int amount)
        {
            hydration -= amount;
#if UNITY_EDITOR
            Debug.Log($"[HydrationManager] : 플레이어의 수분을 {amount}만큼 감소시킵니다. 현재 수분: {hydration}");
#endif

            CheckCurrentHydration();
        }

        private void CheckCurrentHydration()
        {
            HydrationState prevState = currentHydrationState;
            if (hydration >= moistThreshold)
            {
                currentHydrationState = HydrationState.Moist;
            }
            else if (hydration <= dehydratedThreshold)
            {
                currentHydrationState = HydrationState.Dehydrated;
            }
            else if (hydration <= thirstyThreshold)
            {
                currentHydrationState = HydrationState.Thirsty;
            }
            else
                currentHydrationState = HydrationState.Normal;

            if (prevState != currentHydrationState)
            {
                OnHydrationStateChanged?.Invoke(prevState, currentHydrationState);
#if UNITY_EDITOR
                Debug.Log($"[HydrationManager] : 수분 상태가 '{currentHydrationState}'로 변경되었습니다. prev: {prevState}.");
#endif
            }
        }
    }
}
