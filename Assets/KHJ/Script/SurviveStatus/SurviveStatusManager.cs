using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{

    /// <summary>
    /// 생존 상태 종류를 정의하는 열거형입니다. 체온, 정신력, 수분, 포만감 순서입니다.
    /// 포션 효과 등 다른 시스템에서 어떤 생존 상태를 대상으로 하는지 가리킬 때 사용합니다.
    /// </summary>
    public enum SurviveStatusType
    {
        None,
        BodyTemperature,
        MentalStrength,
        Hydration,
        Satiation,
    }

    /// <summary>
    /// 생존 상태를 관리하는 클래스입니다. 체온, 정신력, 수분, 포만감 상태를 관리합니다.
    /// </summary>
    public class SurviveStatusManager : MonoBehaviour
    {

        // ==================== 싱글턴 & 스태틱 설정 ============================
        // ==================== 언제든지 생존상태 데이터에 접근할 수 있도록 ============================
        public static SurviveStatusManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [SerializeField, Required("체온 매니저를 연결해 주세요.")]
        private BodyTemperatureManager bodyTemperatureManager;
        [SerializeField, Required("정신력 매니저를 연결해 주세요.")]
        private MentalStrengthManager mentalStrengthManager;
        [SerializeField, Required("수분 매니저를 연결해 주세요.")]
        private HydrationManager hydrationManager;
        [SerializeField, Required("포만감 매니저를 연결해 주세요.")]
        private SatiationManager satiationManager;

        // 다른 시스템이 Awake 에서 이벤트를 구독할 수 있도록, 초기화는 Start 에서 일괄 수행합니다.
        private void Start()
        {
            ResetAllStatus();
        }

        /// <summary>
        /// 모든 생존 상태를 초기화합니다. 체온, 정신력, 수분, 포만감 상태를 초기값으로 재설정합니다.
        /// </summary>
        [Button, LabelText("모든 상태 초기화")]
        public void ResetAllStatus()
        {
            bodyTemperatureManager.ResetBodyTemperature();
            mentalStrengthManager.ResetMentalStrength();
            hydrationManager.ResetHydration();
            satiationManager.ResetSatiation();
        }
    }
}