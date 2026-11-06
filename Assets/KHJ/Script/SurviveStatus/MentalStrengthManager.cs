using System;
using Sirenix.OdinInspector;
using UnityEngine;



namespace Enzeun.Runtime
{
    /// <summary>
    /// 정신력 상태를 명시하는 Enum. 구간이 서로 대체되는 관계입니다.
    /// </summary>
    public enum MentalState
    {
        Normal,
        Smart,
        What,
        Confused,
        GoToDie,
    }

    /// <summary>
    /// 정신력 상태를 관리하는 클래스입니다. 정신력 구간이 바뀔 때, 그리고 0이 되어 사망해야 할 때 이벤트를 발생시킵니다.
    /// </summary>
    public class MentalStrengthManager : MonoBehaviour
    {
        // 기본값 : 50%
        // 구간 판정 : 50% 이하 -> 뭐라고? / 30% 이하 -> 혼란 / 10% 이하 -> 죽어라 / 90% 이상 -> Smart / 0% 이하 -> 사망 이벤트(한 번만)
        // Smart(smartThreshold 이상) -> 똑똑이 버프, 그 외 90 미만 -> 멍청함 디버프는
        // 이 클래스가 아니라 SurviveStatusEffectTable(상태 -> 효과)에서 설정합니다.

        // TODO(미구현 기획): 대미지를 입을 때마다 5% 감소 / 디버프 받을 때마다 5% 감소
        // TODO(미구현 기획): 밝기 3단계 : 50% 이하면 50% 까지 2초당 1% 증가
        // TODO(미구현 기획): 밝기 2단계 : 3초당 1% 감소
        // TODO(미구현 기획): 밝기 1단계 : 3초당 2% 감소


        // 정신력 초기값
        [SerializeField, LabelText("정신력 초기값")]
        private int initialMentalStrength = 50;

        // 똑똑이 버프 임계값
        [SerializeField, LabelText("똑똑이 버프 임계값")]
        private int smartThreshold = 90;

        // 뭐라고? 디버프 임계값
        [SerializeField, LabelText("뭐라고? 디버프 임계값")]
        private int whatDebuffThreshold = 50;

        // 혼란 디버프 임계값
        [SerializeField, LabelText("혼란 디버프 임계값")]
        private int confusionThreshold = 30;

        // 죽어라 디버프 임계값
        [SerializeField, LabelText("죽어라 디버프 임계값")]
        private int goToDieThreshold = 10;

        // 사망 임계값
        [SerializeField, LabelText("사망 임계값")]
        private int deathThreshold = 0;

        // 현재 정신력 상태
        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        public MentalState currentMentalState { get; private set; } = MentalState.Normal;

        // 사망 이벤트를 이미 보냈는지 여부 (0 이하에서 중복 발생 방지)
        private bool isDead = false;

        /// <summary>
        /// 정신력 상태가 변경됨을 알려주는 이벤트. 값 : Prev,Next
        /// </summary>
        public event Action<MentalState, MentalState> OnMentalStateChanged;

        /// <summary>
        /// 플레이어의 정신력이 0이 됨으로, 사망해야 함을 알려주는 이벤트. 한 번만 발생합니다.
        /// </summary>
        public event Action OnDeathThresholdReached;

        private int _mentalStrength;

        /// <summary>
        /// 현재 정신력 수치(0~100). 변경은 증감 메서드로만 합니다.
        /// </summary>
        [ShowInInspector, ReadOnly, PropertyOrder(-2)]
        public int mentalStrength
        {
            get => _mentalStrength;
            private set
            {
                _mentalStrength = Mathf.Clamp(value, 0, 100);
            }
        }

        /// <summary>
        /// 값을 초기값으로 되돌리고 구간을 다시 판정합니다. 구간이 같으면 이벤트는 발생하지 않습니다.
        /// </summary>
        public void ResetMentalStrength()
        {
            isDead = false;
            _mentalStrength = initialMentalStrength;
#if UNITY_EDITOR
            Debug.Log($"[MentalStrengthManager] : 플레이어의 정신력을 초기값 {initialMentalStrength}으로 재설정 합니다.");
#endif
            CheckCurrentMentalStrength();
        }

        /// <summary>
        /// amount(양수)만큼 증가 후 0~100 으로 제한하고 구간을 다시 판정합니다.
        /// </summary>
        [Button, LabelText("정신력 증가")]
        public void IncreaseMentalStrength(int amount)
        {
            mentalStrength += amount;
#if UNITY_EDITOR
            Debug.Log($"[MentalStrengthManager] : 플레이어의 정신력을 {amount}만큼 증가시킵니다. 현재 정신력: {mentalStrength}");
#endif

            CheckCurrentMentalStrength();
        }

        /// <summary>
        /// amount(양수)만큼 감소 후 0~100 으로 제한하고 구간을 다시 판정합니다.
        /// </summary>
        [Button, LabelText("정신력 감소")]
        public void DecreaseMentalStrength(int amount)
        {
            mentalStrength -= amount;
#if UNITY_EDITOR
            Debug.Log($"[MentalStrengthManager] : 플레이어의 정신력을 {amount}만큼 감소시킵니다. 현재 정신력: {mentalStrength}");
#endif

            CheckCurrentMentalStrength();
        }

        /// <summary>
        /// 현재 수치로 구간을 판정합니다. 구간이 바뀐 경우에만 이벤트가 발생합니다. 0 이하가 되면 사망 이벤트도 한 번 발생시킵니다.
        /// </summary>
        private void CheckCurrentMentalStrength()
        {
            MentalState prevState = currentMentalState;
            if (mentalStrength >= smartThreshold)
            {
                currentMentalState = MentalState.Smart;
            }
            else if (mentalStrength <= goToDieThreshold)
            {
                currentMentalState = MentalState.GoToDie;
            }
            else if (mentalStrength <= confusionThreshold)
            {
                currentMentalState = MentalState.Confused;
            }
            else if (mentalStrength <= whatDebuffThreshold)
            {
                currentMentalState = MentalState.What;
            }
            else
                currentMentalState = MentalState.Normal;

            if (prevState != currentMentalState)
            {
                OnMentalStateChanged?.Invoke(prevState, currentMentalState);
#if UNITY_EDITOR
                Debug.Log($"[MentalStrengthManager] : 정신력 상태가 '{currentMentalState}'로 변경되었습니다. prev: {prevState}.");
#endif
            }

            // 사망 판정은 상태 변화와 별개로, 0 이하가 되는 순간 한 번만 알립니다.
            if (!isDead && mentalStrength <= deathThreshold)
            {
                isDead = true;
                OnDeathThresholdReached?.Invoke();
#if UNITY_EDITOR
                Debug.Log("[MentalStrengthManager] : 정신력이 0이 되어 사망 이벤트를 발생시킵니다.");
#endif
            }
        }
    }

}
