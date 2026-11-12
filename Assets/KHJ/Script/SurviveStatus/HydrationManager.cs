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
        // 구간 판정 : 70% 이상 -> 촉촉함 버프 / 10% 이하 -> 갈증 디버프 / 0% -> 탈수 디버프(갈증보다 우선)

        // 5초당 1% 자연 감소(더위 중에는 2배)는 SurviveStatusManager 가 시간을 재서 DecreaseHydration 으로 처리합니다.


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

        /// <summary>
        /// 현재 수분 수치(0~100). 변경은 증감 메서드로만 합니다.
        /// </summary>
        [ShowInInspector, ReadOnly, PropertyOrder(-2)]
        public int hydration
        {
            get => _hydration;
            private set
            {
                _hydration = Mathf.Clamp(value, 0, 100);
            }
        }

        /// <summary>
        /// 값을 초기값으로 되돌리고 구간을 다시 판정합니다. 구간이 같으면 이벤트는 발생하지 않습니다.
        /// </summary>
        public void ResetHydration()
        {
            _hydration = initialHydration;
#if UNITY_EDITOR
            Debug.Log($"[HydrationManager] : 플레이어의 수분을 초기값 {initialHydration}으로 재설정 합니다.");
#endif
            CheckCurrentHydration();
        }

        /// <summary>
        /// amount(양수)만큼 증가 후 0~100 으로 제한하고 구간을 다시 판정합니다.
        /// </summary>
        [Button, LabelText("수분 증가")]
        public void IncreaseHydration(int amount)
        {
            hydration += amount;
#if UNITY_EDITOR
            Debug.Log($"[HydrationManager] : 플레이어의 수분을 {amount}만큼 증가시킵니다. 현재 수분: {hydration}");
#endif

            CheckCurrentHydration();
        }

        /// <summary>
        /// amount(양수)만큼 감소 후 0~100 으로 제한하고 구간을 다시 판정합니다.
        /// </summary>
        [Button, LabelText("수분 감소")]
        public void DecreaseHydration(int amount)
        {
            hydration -= amount;
#if UNITY_EDITOR
            Debug.Log($"[HydrationManager] : 플레이어의 수분을 {amount}만큼 감소시킵니다. 현재 수분: {hydration}");
#endif

            CheckCurrentHydration();
        }

        /// <summary>
        /// 현재 수치로 구간을 판정합니다. 구간이 바뀐 경우에만 이벤트가 발생합니다.
        /// </summary>
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
