using System.Collections.Generic;
using Enzeun.SO;
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

        // 상태 구간별로 걸리는 버프/디버프 표입니다.
        [SerializeField, Required("구간별 상태 효과 표를 연결해 주세요.")]
        private SurviveStatusEffectTable effectTable;

        // 내 플레이어 전용 컨테이너입니다. 이 매니저와 같은 오브젝트에 붙여서 연결합니다.
        // (Awake 에서 자기 자신을 Owner 로 지정하는 옵션은 끄고, 플레이어가 생성된 뒤 BindPlayer 로 연결)
        [SerializeField, Required("플레이어 전용 BuffContainer 를 연결해 주세요.")]
        private BuffContainer buffContainer;

        /// <summary>
        /// 내 플레이어의 BuffContainer 입니다. 포션 폭발 등에서 "내 플레이어인지" 판정할 때 사용합니다.
        /// </summary>
        public BuffContainer PlayerBuffContainer => buffContainer;

        /// <summary>
        /// 내 플레이어 오브젝트를 연결합니다. 로컬 플레이어가 생성된 뒤에 호출합니다.
        /// 연결하면 체력 증감 효과(탈수 등)가 이 플레이어의 IDamageable 에게 적용됩니다.
        /// </summary>
        public void BindPlayer(GameObject player)
        {
            if (buffContainer == null)
            {
                Debug.LogWarning("[SurviveStatusManager]: BuffContainer 가 연결되지 않아 플레이어를 연결할 수 없습니다.");
                return;
            }

            buffContainer.SetOwner(player);
        }

        private void OnEnable()
        {
            bodyTemperatureManager.OnBodyTemperatureStateChanged += HandleBodyTemperatureStateChanged;
            mentalStrengthManager.OnMentalStateChanged += HandleMentalStateChanged;
            hydrationManager.OnHydrationStateChanged += HandleHydrationStateChanged;
            satiationManager.OnSatiationStateChanged += HandleSatiationStateChanged;
        }

        private void OnDisable()
        {
            bodyTemperatureManager.OnBodyTemperatureStateChanged -= HandleBodyTemperatureStateChanged;
            mentalStrengthManager.OnMentalStateChanged -= HandleMentalStateChanged;
            hydrationManager.OnHydrationStateChanged -= HandleHydrationStateChanged;
            satiationManager.OnSatiationStateChanged -= HandleSatiationStateChanged;
        }

        // 다른 시스템이 Awake 에서 이벤트를 구독할 수 있도록, 초기화는 Start 에서 일괄 수행합니다.
        private void Start()
        {
            ResetAllStatus();
        }

        /// <summary>
        /// 모든 생존 상태를 초기화합니다. 체온, 정신력, 수분, 포만감 상태를 초기값으로 재설정합니다.
        /// 초기 구간의 상태 효과도 다시 겁니다. (구간이 바뀌지 않으면 이벤트가 오지 않아 놓치기 때문)
        /// </summary>
        [Button, LabelText("모든 상태 초기화")]
        public void ResetAllStatus()
        {
            bodyTemperatureManager.ResetBodyTemperature();
            mentalStrengthManager.ResetMentalStrength();
            hydrationManager.ResetHydration();
            satiationManager.ResetSatiation();

            SyncAllEffects();
        }

        // ==================== 구간 -> 상태 효과(버프/디버프) 연결 ============================

        private void HandleBodyTemperatureStateChanged(BodyTemperatureState prev, BodyTemperatureState next)
        {
            SyncEffects(bodyTemperatureManager, effectTable != null ? effectTable.GetEffects(next) : null);
        }

        private void HandleMentalStateChanged(MentalState prev, MentalState next)
        {
            SyncEffects(mentalStrengthManager, effectTable != null ? effectTable.GetEffects(next) : null);
        }

        private void HandleHydrationStateChanged(HydrationState prev, HydrationState next)
        {
            SyncEffects(hydrationManager, effectTable != null ? effectTable.GetEffects(next) : null);
        }

        private void HandleSatiationStateChanged(SatiationState prev, SatiationState next)
        {
            SyncEffects(satiationManager, effectTable != null ? effectTable.GetEffects(next) : null);
        }

        /// <summary>
        /// 현재 구간에 맞는 상태 효과를 네 가지 생존 상태 모두에 대해 다시 맞춥니다.
        /// </summary>
        private void SyncAllEffects()
        {
            if (effectTable == null || buffContainer == null)
            {
                Debug.LogWarning("[SurviveStatusManager]: 상태 효과 표 또는 BuffContainer 가 연결되지 않아 구간별 상태 효과를 적용하지 않습니다.");
                return;
            }

            SyncEffects(bodyTemperatureManager, effectTable.GetEffects(bodyTemperatureManager.currentBodyTemperatureState));
            SyncEffects(mentalStrengthManager, effectTable.GetEffects(mentalStrengthManager.currentMentalState));
            SyncEffects(hydrationManager, effectTable.GetEffects(hydrationManager.currentHydrationState));
            SyncEffects(satiationManager, effectTable.GetEffects(satiationManager.currentSatiationState));
        }

        /// <summary>
        /// 해당 상태 매니저가 건 효과를 모두 빼고, 새 구간의 효과를 모두 겁니다.
        /// 건 주체를 매니저로 두므로 다른 상태나 포션이 건 효과는 영향받지 않습니다.
        /// </summary>
        private void SyncEffects(object source, IReadOnlyList<StatusEffectData> effects)
        {
            if (buffContainer == null)
            {
                return;
            }

            buffContainer.RemoveBySource(source);

            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                buffContainer.Apply(effects[i], source);
            }
        }

        /// <summary>
        /// 지정한 생존 상태를 amount 만큼 증감합니다. 양수는 증가, 음수는 감소입니다.
        /// 모든 생존 상태가 0~100 범위라서 퍼센트와 수치가 같습니다. (예: 수분 +5% -> 5)
        /// </summary>
        public void ChangeStatus(SurviveStatusType type, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            switch (type)
            {
                case SurviveStatusType.BodyTemperature:
                    if (amount > 0) bodyTemperatureManager.IncreaseBodyTemperature(amount);
                    else bodyTemperatureManager.DecreaseBodyTemperature(-amount);
                    break;
                case SurviveStatusType.MentalStrength:
                    if (amount > 0) mentalStrengthManager.IncreaseMentalStrength(amount);
                    else mentalStrengthManager.DecreaseMentalStrength(-amount);
                    break;
                case SurviveStatusType.Hydration:
                    if (amount > 0) hydrationManager.IncreaseHydration(amount);
                    else hydrationManager.DecreaseHydration(-amount);
                    break;
                case SurviveStatusType.Satiation:
                    if (amount > 0) satiationManager.IncreaseSatiation(amount);
                    else satiationManager.DecreaseSatiation(-amount);
                    break;
                default:
                    Debug.LogWarning($"[SurviveStatusManager]: 증감할 생존 상태가 지정되지 않았습니다. ({type})");
                    break;
            }
        }
    }
}