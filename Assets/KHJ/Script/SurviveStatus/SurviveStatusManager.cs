using UnityEngine;


namespace Enzeun.Runtime
{

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

        [SerializeField]
        private BodyTemperatureManager bodyTemperatureManager;

        /// <summary>
        /// 모든 생존 상태를 초기화합니다. 체온, 정신력, 수분, 포만감 상태를 초기값으로 재설정합니다.
        /// </summary>
        public void ResetAllStatus()
        {
            bodyTemperatureManager.ResetBodyTemperature();
        }
    }
}