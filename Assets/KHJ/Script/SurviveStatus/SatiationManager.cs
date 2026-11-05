using System;
using Sirenix.OdinInspector;
using UnityEngine;



namespace Enzeun.Runtime
{
    /// <summary>
    /// 포만감 상태를 명시하는 Enum. 구간이 서로 대체되는 관계입니다.
    /// </summary>
    public enum SatiationState
    {
        Normal,
        Full,
        Hungry,
        Starving,
    }

    /// <summary>
    /// 포만감 상태를 관리하는 클래스입니다. 포만감 구간이 바뀔 때 이벤트를 발생시킵니다.
    /// </summary>
    public class SatiationManager : MonoBehaviour
    {
        // 기본값 : 50%

        // 5초당 1% 감소 (러프)
        // 20% 이하 -> 허기 디버프
        // 0% -> 허기 에서 굶주림 디버프로 변경

        // 90% 이상 -> 배부름 디버프


        // 포만감 초기값
        [SerializeField, LabelText("포만감 초기값")]
        private int initialSatiation = 50;

        // 배부름 디버프 임계값
        [SerializeField, LabelText("배부름 디버프 임계값")]
        private int fullThreshold = 90;

        // 허기 디버프 임계값
        [SerializeField, LabelText("허기 디버프 임계값")]
        private int hungryThreshold = 20;

        // 굶주림 디버프 임계값
        [SerializeField, LabelText("굶주림 디버프 임계값")]
        private int starvingThreshold = 0;

        // 현재 포만감 상태
        public SatiationState currentSatiationState { get; private set; } = SatiationState.Normal;

        /// <summary>
        /// 포만감 상태가 변경됨을 알려주는 이벤트. 값 : Prev,Next
        /// </summary>
        public event Action<SatiationState, SatiationState> OnSatiationStateChanged;

        private int _satiation;
        public int satiation
        {
            get => _satiation;
            private set
            {
                _satiation = Mathf.Clamp(value, 0, 100);
            }
        }

        public void ResetSatiation()
        {
            _satiation = initialSatiation;
#if UNITY_EDITOR
            Debug.Log($"[SatiationManager] : 플레이어의 포만감을 초기값 {initialSatiation}으로 재설정 합니다.");
#endif
            CheckCurrentSatiation();
        }

        public void IncreaseSatiation(int amount)
        {
            satiation += amount;
#if UNITY_EDITOR
            Debug.Log($"[SatiationManager] : 플레이어의 포만감을 {amount}만큼 증가시킵니다. 현재 포만감: {satiation}");
#endif

            CheckCurrentSatiation();
        }

        public void DecreaseSatiation(int amount)
        {
            satiation -= amount;
#if UNITY_EDITOR
            Debug.Log($"[SatiationManager] : 플레이어의 포만감을 {amount}만큼 감소시킵니다. 현재 포만감: {satiation}");
#endif

            CheckCurrentSatiation();
        }

        private void CheckCurrentSatiation()
        {
            SatiationState prevState = currentSatiationState;
            if (satiation >= fullThreshold)
            {
                currentSatiationState = SatiationState.Full;
            }
            else if (satiation <= starvingThreshold)
            {
                currentSatiationState = SatiationState.Starving;
            }
            else if (satiation <= hungryThreshold)
            {
                currentSatiationState = SatiationState.Hungry;
            }
            else
                currentSatiationState = SatiationState.Normal;

            if (prevState != currentSatiationState)
            {
                OnSatiationStateChanged?.Invoke(prevState, currentSatiationState);
#if UNITY_EDITOR
                Debug.Log($"[SatiationManager] : 포만감 상태가 '{currentSatiationState}'로 변경되었습니다. prev: {prevState}.");
#endif
            }
        }
    }
}
