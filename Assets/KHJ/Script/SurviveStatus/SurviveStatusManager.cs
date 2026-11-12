using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        // ==================== 싱글턴 (어디서든 생존 상태에 접근) ============================
        public static SurviveStatusManager Instance { get; private set; }

        /// <summary>
        /// 싱글턴 등록, 중복 인스턴스는 제거합니다.
        /// </summary>
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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
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

        // 내 플레이어 전용 컨테이너입니다. 이 매니저와 같은 오브젝트에 붙이고, 플레이어 생성 후 BindPlayer 로 Owner 를 연결합니다.
        [SerializeField, Required("플레이어 전용 BuffContainer 를 연결해 주세요.")]
        private BuffContainer buffContainer;

        /// <summary>
        /// 내 플레이어의 BuffContainer 입니다. 포션 폭발 등에서 "내 플레이어인지" 판정할 때 사용합니다.
        /// </summary>
        public BuffContainer PlayerBuffContainer => buffContainer;

        // ==================== 자연 감소 / 체온 제한 설정 ============================
        // 기획 : 수분, 포만감은 5초당 1% 감소. 더위 디버프 중에는 수분이, 추위 디버프 중에는 포만감이 2배 빨리 감소한다.
        //        차분함 버프 중에는 체온이 69% 이상 올라가지 않고, 열혈 버프 중에는 체온이 31% 밑으로 내려가지 않는다.

        // 상태 효과 ID 입니다. (데이터 시트 기준)
        private const int CalmBuffId = 60006;       // 차분함
        private const int PassionBuffId = 60007;    // 열혈
        private const int HotDebuffId = 61001;      // 더위
        private const int ColdDebuffId = 61002;     // 추위

        [SerializeField, LabelText("자연 감소 사용"), BoxGroup("자연 감소")]
        private bool _useNaturalDecay = true;

        [SerializeField, LabelText("감소 간격 (초)"), MinValue(0.1f), BoxGroup("자연 감소")]
        private float _decayIntervalSeconds = 5f;

        [SerializeField, LabelText("1회 감소량"), MinValue(0), BoxGroup("자연 감소")]
        private int _decayAmount = 1;

        [SerializeField, LabelText("더위/추위 중 감소 속도 배율"), MinValue(1f), BoxGroup("자연 감소")]
        private float _extremeDecayMultiplier = 2f;

        [SerializeField, LabelText("차분함 체온 상한 (%)"), Range(0, 100), BoxGroup("체온 제한")]
        private int _calmMaxTemperature = 69;

        [SerializeField, LabelText("열혈 체온 하한 (%)"), Range(0, 100), BoxGroup("체온 제한")]
        private int _passionMinTemperature = 31;

        // 감소 진행도(초 × 속도 배율)입니다. 간격에 도달할 때마다 한 번 감소하고 남은 만큼은 이어서 쌓습니다.
        private float _hydrationProgress;
        private float _satiationProgress;

        // 현재 감소 속도 배율입니다. 상태 효과가 바뀔 때만 다시 계산합니다.
        private float _hydrationRate = 1f;
        private float _satiationRate = 1f;

        // 상태 효과 변경 이벤트 안에서 바로 체온을 바꾸면 컨테이너가 효과를 처리하는 도중에 다시 효과가 바뀔 수 있으므로, 다음 프레임에 처리합니다.
        private bool _limitsDirty;

        /// <summary>
        /// 내 플레이어 오브젝트를 연결합니다. 로컬 플레이어가 생성된 뒤에 호출합니다.
        /// 연결하면 체력 증감 효과(탈수 등)가 이 플레이어의 IDamageable 에게 적용됩니다.
        /// </summary>
        public void BindPlayer(GameObject player)
        {
            if (buffContainer == null)
            {
                Debug.LogWarning("[SurviveStatusManager] : BuffContainer 가 연결되지 않아 플레이어를 연결할 수 없습니다.");
                return;
            }

            buffContainer.SetOwner(player);
        }

        /// <summary>
        /// 각 생존 상태의 구간 변경 이벤트와 주기 행동 이벤트를 구독합니다.
        /// </summary>
        private void OnEnable()
        {
            bodyTemperatureManager.OnBodyTemperatureStateChanged += HandleBodyTemperatureStateChanged;
            mentalStrengthManager.OnMentalStateChanged += HandleMentalStateChanged;
            hydrationManager.OnHydrationStateChanged += HandleHydrationStateChanged;
            satiationManager.OnSatiationStateChanged += HandleSatiationStateChanged;

            if (buffContainer != null)
            {
                buffContainer.OnPeriodicAction += HandlePeriodicAction;
                buffContainer.OnEffectAdded += HandleEffectChanged;
                buffContainer.OnEffectRemoved += HandleEffectChanged;
            }

            RefreshDecayRates();
            _limitsDirty = true;
        }

        /// <summary>
        /// OnEnable 에서 구독한 이벤트를 대칭으로 해제합니다.
        /// </summary>
        private void OnDisable()
        {
            bodyTemperatureManager.OnBodyTemperatureStateChanged -= HandleBodyTemperatureStateChanged;
            mentalStrengthManager.OnMentalStateChanged -= HandleMentalStateChanged;
            hydrationManager.OnHydrationStateChanged -= HandleHydrationStateChanged;
            satiationManager.OnSatiationStateChanged -= HandleSatiationStateChanged;

            if (buffContainer != null)
            {
                buffContainer.OnPeriodicAction -= HandlePeriodicAction;
                buffContainer.OnEffectAdded -= HandleEffectChanged;
                buffContainer.OnEffectRemoved -= HandleEffectChanged;
            }
        }

        // 다른 시스템이 Awake 에서 이벤트를 구독할 수 있도록, 초기화는 Start 에서 일괄 수행합니다.
        private void Start()
        {
            // 제거될 중복 인스턴스는 초기화하지 않습니다.
            if (Instance != this)
            {
                return;
            }

            ResetAllStatus();
        }

        private void Update()
        {
            // 제거될 중복 인스턴스는 아무것도 하지 않습니다.
            if (Instance != this)
            {
                return;
            }

            if (_limitsDirty)
            {
                _limitsDirty = false;
                ApplyTemperatureLimits();
            }

            if (_useNaturalDecay)
            {
                float deltaTime = Time.deltaTime;
                TickDecay(ref _hydrationProgress, _hydrationRate, deltaTime, SurviveStatusType.Hydration);
                TickDecay(ref _satiationProgress, _satiationRate, deltaTime, SurviveStatusType.Satiation);
            }
        }

        // ==================== 자연 감소 ============================

        /// <summary>
        /// 감소 진행도를 쌓고, 간격에 도달할 때마다 한 번씩 감소시킵니다. (프레임이 길어도 놓치지 않도록 반복)
        /// </summary>
        private void TickDecay(ref float progress, float rate, float deltaTime, SurviveStatusType type)
        {
            float interval = Mathf.Max(0.1f, _decayIntervalSeconds);
            progress += deltaTime * rate;

            while (progress >= interval)
            {
                progress -= interval;
                ChangeStatus(type, -_decayAmount);
            }
        }

        /// <summary>
        /// 더위/추위 디버프가 걸려 있는지 보고 감소 속도 배율을 다시 정합니다. (더위 : 수분, 추위 : 포만감)
        /// </summary>
        private void RefreshDecayRates()
        {
            _hydrationRate = HasEffect(HotDebuffId) ? _extremeDecayMultiplier : 1f;
            _satiationRate = HasEffect(ColdDebuffId) ? _extremeDecayMultiplier : 1f;
        }

        /// <summary>
        /// 차분함/열혈 버프가 걸려 있는지 보고 체온 범위를 정합니다. 현재 체온이 범위를 벗어나 있으면 범위 안으로 맞춥니다.
        /// (예: 체온 80 에서 차분함을 받으면 69 가 되어 더움 구간에서 벗어남)
        /// </summary>
        private void ApplyTemperatureLimits()
        {
            int max = HasEffect(CalmBuffId) ? _calmMaxTemperature : 100;
            int min = HasEffect(PassionBuffId) ? _passionMinTemperature : 0;
            bodyTemperatureManager.SetLimits(min, max);
        }

        /// <summary>
        /// 내 플레이어에게 해당 ID 의 상태 효과(버프 또는 디버프)가 걸려 있는지 확인합니다.
        /// </summary>
        private bool HasEffect(int effectId)
        {
            if (buffContainer == null)
            {
                return false;
            }

            for (int i = 0; i < buffContainer.buffs.Count; i++)
            {
                if (buffContainer.buffs[i].data.ID == effectId)
                {
                    return true;
                }
            }

            for (int i = 0; i < buffContainer.debuffs.Count; i++)
            {
                if (buffContainer.debuffs[i].data.ID == effectId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 상태 효과가 걸리거나 빠졌을 때 감소 속도 배율은 바로, 체온 범위는 다음 프레임에 다시 정합니다.
        /// </summary>
        private void HandleEffectChanged(ActiveStatusEffect effect)
        {
            RefreshDecayRates();
            _limitsDirty = true;
        }

        /// <summary>
        /// 모든 생존 상태를 초기값으로 재설정하고, 초기 구간의 상태 효과도 다시 겁니다.
        /// (구간이 바뀌지 않으면 이벤트가 오지 않기 때문)
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

        /// <summary>
        /// 생존 상태(체온, 정신력, 수분, 포만감)의 수치는 그대로 두고, 현재 구간에 맞는 상태 효과만 다시 맞춥니다.
        /// 플레이어 생성이나 부활처럼 BuffContainer 를 비운 뒤에 부르면, 구간이 건 효과(예: 정신력 초기값 50 의 멍청함)가 되살아납니다.
        /// 이미 걸린 효과는 다시 걸지 않고, 비어서 빠진 효과만 새로 겁니다.
        /// </summary>
        public void ResyncStatusEffects()
        {
            SyncAllEffects();

            // 컨테이너를 비운 뒤라면 감소 배율과 체온 범위도 지금 효과 기준으로 다시 맞춥니다.
            RefreshDecayRates();
            _limitsDirty = true;
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
                Debug.LogWarning("[SurviveStatusManager] : 상태 효과 표 또는 BuffContainer 가 연결되지 않아 구간별 상태 효과를 적용하지 않습니다.");
                return;
            }

            SyncEffects(bodyTemperatureManager, effectTable.GetEffects(bodyTemperatureManager.currentBodyTemperatureState));
            SyncEffects(mentalStrengthManager, effectTable.GetEffects(mentalStrengthManager.currentMentalState));
            SyncEffects(hydrationManager, effectTable.GetEffects(hydrationManager.currentHydrationState));
            SyncEffects(satiationManager, effectTable.GetEffects(satiationManager.currentSatiationState));
        }

        // 새 구간 목록에 없어서 제거할 효과를 모아 두는 재사용 버퍼입니다.
        private readonly List<StatusEffectData> removeBuffer = new List<StatusEffectData>();

        /// <summary>
        /// 해당 상태 매니저(source)가 건 효과를 새 구간의 목록과 맞춥니다. 목록에 없는 효과만 빼고, 안 걸린 효과만 새로 겁니다.
        /// 유지되는 효과는 다시 걸지 않아 주기 행동 타이머가 초기화되지 않으며, 다른 상태나 포션이 건 효과는 영향받지 않습니다.
        /// </summary>
        private void SyncEffects(object source, IReadOnlyList<StatusEffectData> effects)
        {
            if (buffContainer == null)
            {
                return;
            }

            removeBuffer.Clear();
            CollectRemovedEffects(buffContainer.buffs, source, effects);
            CollectRemovedEffects(buffContainer.debuffs, source, effects);

            for (int i = 0; i < removeBuffer.Count; i++)
            {
                buffContainer.Remove(removeBuffer[i], source);
            }

            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                if (!IsAppliedBy(buffContainer.buffs, source, effects[i]) && !IsAppliedBy(buffContainer.debuffs, source, effects[i]))
                {
                    buffContainer.Apply(effects[i], source);
                }
            }
        }

        /// <summary>
        /// 해당 주체가 건 효과 중 새 목록에 없는 것을 removeBuffer 에 모읍니다.
        /// </summary>
        private void CollectRemovedEffects(IReadOnlyList<ActiveStatusEffect> active, object source, IReadOnlyList<StatusEffectData> effects)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (!Equals(active[i].source, source))
                {
                    continue;
                }

                bool isKept = false;
                if (effects != null)
                {
                    for (int j = 0; j < effects.Count; j++)
                    {
                        if (effects[j] == active[i].data)
                        {
                            isKept = true;
                            break;
                        }
                    }
                }

                if (!isKept)
                {
                    removeBuffer.Add(active[i].data);
                }
            }
        }

        /// <summary>
        /// 해당 주체가 이미 이 효과를 걸어 두었는지 확인합니다.
        /// </summary>
        private static bool IsAppliedBy(IReadOnlyList<ActiveStatusEffect> active, object source, StatusEffectData data)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].data == data && Equals(active[i].source, source))
                {
                    return true;
                }
            }

            return false;
        }

        // ==================== 주기 행동 (BuffContainer 가 발동 시점을 알려 주면 여기서 처리) ============================

        /// <summary>
        /// 주기 행동이 발동했을 때 호출됩니다. 컨테이너는 발동 시점만 관리하므로, 실제 행동은 여기서 처리합니다.
        /// </summary>
        private void HandlePeriodicAction(ActiveStatusEffect effect, PeriodicActionModule module)
        {
#if UNITY_EDITOR
            Debug.Log($"[SurviveStatusManager] : 주기 행동 발동 {effect.data.statusEffectName} / {module.ActionType}");
#endif

            // StopPlayerMove(추위), DrinkHeldPotion(갈증)은 플레이어 몸(이동, 손)을 다루는 행동이라 PlayerStatusEffects 가 같은 이벤트를 받아 처리합니다.
            if (module.ActionType == PeriodicActionType.ConsumeFirstItem)
            {
                ConsumeFirstItemAsync(module.Amount).Forget();
            }
        }

        /// <summary>
        /// 내 개인 인벤토리의 첫 번째 아이템을 소모하고, 소모에 성공했을 때만 포만감을 채웁니다. (허기)
        /// 소모는 마스터가 확정하므로 결과를 기다립니다. 개인 인벤토리가 비어 있으면(NotFound) 아무 일도 일어나지 않습니다.
        /// </summary>
        private async UniTaskVoid ConsumeFirstItemAsync(int satiationAmount)
        {
            InventoryNetworkSyncManager inventory = InventoryNetworkSyncManager.Instance;
            if (inventory == null)
            {
                return;
            }

            InventoryResult result = await inventory.ConsumeFirstPersonalAsync();

            // 기다리는 사이 이 매니저가 사라졌으면 더 할 것이 없습니다.
            if (this == null || !result.Success)
            {
                return;
            }

            ChangeStatus(SurviveStatusType.Satiation, satiationAmount);
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
                    Debug.LogWarning($"[SurviveStatusManager] : 증감할 생존 상태가 지정되지 않았습니다. ({type})");
                    break;
            }
        }
    }
}