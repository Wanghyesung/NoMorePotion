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

        #region Singleton
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
        #endregion

        #region Refs
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
        #endregion

        #region DecayConfig
        // ==================== 자연 감소 / 체온 제한 설정 ============================
        // 기획 : 수분, 포만감은 5초당 1% 감소. 더위 디버프 중에는 수분이, 추위 디버프 중에는 포만감이 2배 빨리 감소한다.
        //        차분함 버프 중에는 체온이 69% 이상 올라가지 않고, 열혈 버프 중에는 체온이 31% 밑으로 내려가지 않는다.

        // 상태 효과 ID 입니다. (데이터 시트 기준)
        private const int CalmBuffId = 60006;       // 차분함
        private const int PassionBuffId = 60007;    // 열혈
        private const int HotDebuffId = 61001;      // 더위
        private const int ColdDebuffId = 61002;     // 추위

        // 수분과 포만감은 각자 따로 조절합니다. (값은 틱마다 읽으므로 Play 중에 인스펙터에서 바꿔도 바로 반영)
        [SerializeField, LabelText("자연 감소 사용 (수분, 포만감 모두)"), BoxGroup("자연 감소")]
        private bool _useNaturalDecay = true;

        [SerializeField, LabelText("감소 간격 (초)"), MinValue(0.1f), BoxGroup("자연 감소/수분")]
        private float _hydrationDecayIntervalSeconds = 15f;

        [SerializeField, LabelText("1회 감소량"), MinValue(0), BoxGroup("자연 감소/수분")]
        private int _hydrationDecayAmount = 1;

        [SerializeField, LabelText("더위 중 감소 속도 배율"), MinValue(1f), BoxGroup("자연 감소/수분")]
        private float _hotHydrationDecayMultiplier = 2f;

        [SerializeField, LabelText("감소 간격 (초)"), MinValue(0.1f), BoxGroup("자연 감소/포만감")]
        private float _satiationDecayIntervalSeconds = 15f;

        [SerializeField, LabelText("1회 감소량"), MinValue(0), BoxGroup("자연 감소/포만감")]
        private int _satiationDecayAmount = 1;

        [SerializeField, LabelText("추위 중 감소 속도 배율"), MinValue(1f), BoxGroup("자연 감소/포만감")]
        private float _coldSatiationDecayMultiplier = 2f;

        [SerializeField, LabelText("차분함 체온 상한 (%)"), Range(0, 100), BoxGroup("체온 제한")]
        private int _calmMaxTemperature = 69;

        [SerializeField, LabelText("열혈 체온 하한 (%)"), Range(0, 100), BoxGroup("체온 제한")]
        private int _passionMinTemperature = 31;

        // 감소 진행도(초 × 속도 배율)입니다. 간격에 도달할 때마다 한 번 감소하고 남은 만큼은 이어서 쌓습니다.
        private float _hydrationProgress;
        private float _satiationProgress;

        // 더위/추위 디버프가 걸려 있는지입니다. 상태 효과가 바뀔 때만 다시 확인하고, 배율은 틱마다 인스펙터 값을 읽습니다.
        private bool _isHot;
        private bool _isCold;

        // 상태 효과 변경 이벤트 안에서 바로 체온을 바꾸면 컨테이너가 효과를 처리하는 도중에 다시 효과가 바뀔 수 있으므로, 다음 프레임에 처리합니다.
        private bool _limitsDirty;
        #endregion

        #region MentalConfig
        // ==================== 정신력 변화 설정 ============================
        // 시트 : 기본값 50%. 대미지를 입을 때마다 5% 감소. 0% 가 되면 즉사.
        //        밝기 3단계(기본 환경) : 50% 이하이면 50% 까지 2초당 1% 증가 / 밝기 2단계 : 5초당 1% 감소 / 밝기 1단계 : 5초당 2% 감소
        // 값은 틱마다 읽으므로 Play 중에 인스펙터에서 바꿔도 바로 반영됩니다.

        [SerializeField, LabelText("정신력 변화 사용"), BoxGroup("정신력")]
        private bool _useMentalChange = true;

        [SerializeField, LabelText("시작 밝기 환경"), BoxGroup("정신력")]
        private BrightnessLevel _initialBrightness = BrightnessLevel.Bright;

        [SerializeField, LabelText("밝음(3단계) 회복 간격 (초)"), MinValue(0.1f), BoxGroup("정신력/밝기 3단계")]
        private float _brightRecoverIntervalSeconds = 2f;

        [SerializeField, LabelText("밝음(3단계) 1회 회복량"), MinValue(0), BoxGroup("정신력/밝기 3단계")]
        private int _brightRecoverAmount = 1;

        [SerializeField, LabelText("밝음(3단계) 회복 상한 (%)"), Range(0, 100), BoxGroup("정신력/밝기 3단계")]
        private int _brightRecoverUpTo = 50;

        [SerializeField, LabelText("어두움(2단계) 감소 간격 (초)"), MinValue(0.1f), BoxGroup("정신력/밝기 2단계")]
        private float _dimDecayIntervalSeconds = 5f;

        [SerializeField, LabelText("어두움(2단계) 1회 감소량"), MinValue(0), BoxGroup("정신력/밝기 2단계")]
        private int _dimDecayAmount = 1;

        [SerializeField, LabelText("매우 어두움(1단계) 감소 간격 (초)"), MinValue(0.1f), BoxGroup("정신력/밝기 1단계")]
        private float _darkDecayIntervalSeconds = 5f;

        [SerializeField, LabelText("매우 어두움(1단계) 1회 감소량"), MinValue(0), BoxGroup("정신력/밝기 1단계")]
        private int _darkDecayAmount = 2;

        [SerializeField, LabelText("대미지를 입을 때마다 감소 (%)"), MinValue(0), BoxGroup("정신력/피격")]
        private int _damageMentalLoss = 5;

        // 탈수, 굶주림, 범위 장판 같은 주기 대미지는 틱마다 계속 들어와서, 포함하면 정신력이 순식간에 0 이 되어 즉사합니다. 기본은 제외입니다.
        [SerializeField, LabelText("주기 대미지(탈수, 장판 틱)도 감소에 포함"), BoxGroup("정신력/피격")]
        private bool _periodicDamageReducesMental = false;

        /// <summary>
        /// 지금 플레이어가 있는 곳의 밝기 환경 단계입니다. 정신력이 변하는 방식을 정합니다.
        /// 바꾸는 것은 SetBrightness 로만 합니다. (예: 동굴에 들어가면 Dim 이나 Dark 로)
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("현재 밝기 환경"), BoxGroup("정신력"), PropertyOrder(-10)]
        public BrightnessLevel CurrentBrightness { get; private set; } = BrightnessLevel.Bright;

        /// <summary>
        /// 밝기 환경 단계가 바뀔 때 발생합니다. 값 : Prev, Next
        /// </summary>
        public event System.Action<BrightnessLevel, BrightnessLevel> OnBrightnessChanged;

        // 정신력 변화 진행도(초)입니다. 간격에 도달할 때마다 한 번 변하고 남은 만큼은 이어서 쌓습니다. 밝기 환경이 바뀌면 0 부터 다시 셉니다.
        private float _mentalProgress;

        // 대미지를 입을 때마다 정신력을 깎기 위해 구독하는 내 플레이어의 FSM 입니다.
        private FSM _boundFsm;

        // 정신력이 0 이 되면 즉사시키는 대미지입니다. 방어력으로 나눠도 체력을 넘도록 충분히 크게 둡니다.
        private const int LethalDamage = 1000000;
        #endregion

        #region TemperatureConfig
        // 시트 : 용암지대(더움)는 체온 상승, 빙하지대(추움)는 체온 감소. 기본 지역에서는 기본값(50%)으로 천천히 돌아옵니다.
        // 값은 틱마다 읽으므로 Play 중에 인스펙터에서 바꿔도 바로 반영됩니다.

        [SerializeField, LabelText("체온 변화 사용"), BoxGroup("체온")]
        private bool _useTemperatureChange = true;

        [SerializeField, LabelText("시작 온도 환경"), BoxGroup("체온")]
        private TemperatureEnvironment _initialTemperatureEnvironment = TemperatureEnvironment.Normal;

        [SerializeField, LabelText("더운 지역 상승 간격 (초)"), MinValue(0.1f), BoxGroup("체온/더운 지역")]
        private float _hotRiseIntervalSeconds = 3f;

        [SerializeField, LabelText("더운 지역 1회 상승량"), MinValue(0), BoxGroup("체온/더운 지역")]
        private int _hotRiseAmount = 1;

        [SerializeField, LabelText("추운 지역 하락 간격 (초)"), MinValue(0.1f), BoxGroup("체온/추운 지역")]
        private float _coldFallIntervalSeconds = 3f;

        [SerializeField, LabelText("추운 지역 1회 하락량"), MinValue(0), BoxGroup("체온/추운 지역")]
        private int _coldFallAmount = 1;

        [SerializeField, LabelText("기본 지역 복귀 간격 (초)"), MinValue(0.1f), BoxGroup("체온/기본 지역")]
        private float _normalRecoverIntervalSeconds = 5f;

        [SerializeField, LabelText("기본 지역 1회 복귀량"), MinValue(0), BoxGroup("체온/기본 지역")]
        private int _normalRecoverAmount = 1;

        [SerializeField, LabelText("기본 지역 복귀 목표 (%)"), Range(0, 100), BoxGroup("체온/기본 지역")]
        private int _normalRecoverTarget = 50;

        /// <summary>
        /// 지금 플레이어가 있는 지역의 온도 환경입니다. 체온이 변하는 방향을 정합니다.
        /// 바꾸는 것은 SetTemperatureEnvironment 로만 합니다. (예: 용암지대에 들어가면 Hot, 나오면 Normal)
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("현재 온도 환경"), BoxGroup("체온"), PropertyOrder(-10)]
        public TemperatureEnvironment CurrentTemperatureEnvironment { get; private set; } = TemperatureEnvironment.Normal;

        /// <summary>
        /// 온도 환경이 바뀔 때 발생합니다. 값 : Prev, Next
        /// </summary>
        public event System.Action<TemperatureEnvironment, TemperatureEnvironment> OnTemperatureEnvironmentChanged;

        // 체온 변화 진행도(초)입니다. 간격에 도달할 때마다 한 번 변하고 남은 만큼은 이어서 쌓습니다. 온도 환경이 바뀌면 0 부터 다시 셉니다.
        private float _temperatureProgress;
        #endregion

        #region PlayerBind
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

            // 대미지를 입을 때마다 정신력을 깎기 위해 내 플레이어의 피격 이벤트를 구독합니다. (다시 연결하면 이전 구독을 먼저 해제)
            UnsubscribePlayerHit();
            _boundFsm = player != null ? player.GetComponent<FSM>() : null;
            SubscribePlayerHit();
        }

        private void SubscribePlayerHit()
        {
            if (_boundFsm != null)
            {
                // 중복 구독을 막기 위해 먼저 빼고 넣습니다.
                _boundFsm.OnHitAction -= HandlePlayerHit;
                _boundFsm.OnHitAction += HandlePlayerHit;
            }
        }

        private void UnsubscribePlayerHit()
        {
            if (_boundFsm != null)
            {
                _boundFsm.OnHitAction -= HandlePlayerHit;
            }
        }
        #endregion

        #region Lifecycle
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

            mentalStrengthManager.OnDeathThresholdReached += HandleMentalDeath;
            SubscribePlayerHit();

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
            mentalStrengthManager.OnDeathThresholdReached -= HandleMentalDeath;
            UnsubscribePlayerHit();

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

            CurrentBrightness = _initialBrightness;
            CurrentTemperatureEnvironment = _initialTemperatureEnvironment;
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

                TickDecay(ref _hydrationProgress, _isHot ? _hotHydrationDecayMultiplier : 1f, deltaTime,
                    _hydrationDecayIntervalSeconds, _hydrationDecayAmount, SurviveStatusType.Hydration);

                TickDecay(ref _satiationProgress, _isCold ? _coldSatiationDecayMultiplier : 1f, deltaTime,
                    _satiationDecayIntervalSeconds, _satiationDecayAmount, SurviveStatusType.Satiation);
            }

            if (_useMentalChange)
            {
                TickMental(Time.deltaTime);
            }

            if (_useTemperatureChange)
            {
                TickTemperature(Time.deltaTime);
            }
        }
        #endregion

        #region Mental
        // ==================== 정신력 ============================

        /// <summary>
        /// 밝기 환경 단계를 바꿉니다. (예: 동굴에 들어가면 Dim 이나 Dark, 나오면 Bright) 같은 값이면 아무 일도 없습니다.
        /// 단계가 바뀌면 변화 진행도를 0 부터 다시 세고 OnBrightnessChanged 를 발생시킵니다.
        /// </summary>
        [Button("테스트: 밝기 환경 바꾸기")]
        public void SetBrightness(BrightnessLevel level)
        {
            if (level == CurrentBrightness)
            {
                return;
            }

            BrightnessLevel previous = CurrentBrightness;
            CurrentBrightness = level;
            _mentalProgress = 0f;

#if UNITY_EDITOR
            Debug.Log($"[SurviveStatusManager] : 밝기 환경이 {previous} -> {level} 로 바뀌었습니다.");
#endif

            OnBrightnessChanged?.Invoke(previous, level);
        }

        /// <summary>
        /// 밝기 환경에 따라 정신력을 시간에 맞춰 바꿉니다.
        /// - Bright : 정신력이 회복 상한(기본 50%) 미만일 때만 상한까지 회복합니다. 상한 이상이면 변하지 않습니다.
        /// - Dim, Dark : 정신력이 계속 감소합니다.
        /// 이미 0 이하이면(즉사 처리됨) 더 변하지 않습니다.
        /// </summary>
        private void TickMental(float deltaTime)
        {
            int current = mentalStrengthManager.mentalStrength;

            if (current <= 0)
            {
                _mentalProgress = 0f;
                return;
            }

            switch (CurrentBrightness)
            {
                case BrightnessLevel.Bright:
                    if (current >= _brightRecoverUpTo)
                    {
                        _mentalProgress = 0f;
                        return;
                    }

                    TickMentalChange(deltaTime, _brightRecoverIntervalSeconds, _brightRecoverAmount, _brightRecoverUpTo);
                    break;

                case BrightnessLevel.Dim:
                    TickMentalChange(deltaTime, _dimDecayIntervalSeconds, -_dimDecayAmount, 0);
                    break;

                case BrightnessLevel.Dark:
                    TickMentalChange(deltaTime, _darkDecayIntervalSeconds, -_darkDecayAmount, 0);
                    break;
            }
        }

        /// <summary>
        /// 진행도를 쌓고, 간격에 도달할 때마다 signedAmount 만큼 바꿉니다. (양수는 limit 까지만 회복, 음수는 감소)
        /// </summary>
        private void TickMentalChange(float deltaTime, float intervalSeconds, int signedAmount, int limit)
        {
            float interval = Mathf.Max(0.1f, intervalSeconds);
            _mentalProgress += deltaTime;

            while (_mentalProgress >= interval)
            {
                _mentalProgress -= interval;

                if (signedAmount > 0)
                {
                    int room = limit - mentalStrengthManager.mentalStrength;
                    if (room <= 0)
                    {
                        _mentalProgress = 0f;
                        break;
                    }

                    ChangeStatus(SurviveStatusType.MentalStrength, Mathf.Min(signedAmount, room));
                }
                else if (signedAmount < 0)
                {
                    ChangeStatus(SurviveStatusType.MentalStrength, signedAmount);

                    // 0 이 되면 즉사 처리가 되므로 더 돌지 않습니다.
                    if (mentalStrengthManager.mentalStrength <= 0)
                    {
                        _mentalProgress = 0f;
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 내 플레이어가 대미지를 입을 때마다(치명타 제외) 정신력을 깎습니다. 주기 대미지는 설정에 따라 제외합니다.
        /// </summary>
        private void HandlePlayerHit(CommonStruct.AttackInfo attackInfo)
        {
            if (!_useMentalChange)
            {
                return;
            }

            if (HealthChangeModule.IsApplyingPeriodic && !_periodicDamageReducesMental)
            {
                return;
            }

            ChangeStatus(SurviveStatusType.MentalStrength, -_damageMentalLoss);
        }

        /// <summary>
        /// 정신력이 0 이 되면 내 플레이어를 즉사시킵니다. 일반 대미지와 같은 경로(사망 상태, 시체 상자, 세션 사망 처리)를 탑니다.
        /// </summary>
        private void HandleMentalDeath()
        {
            IDamageable damageable = buffContainer != null ? buffContainer.Damageable : null;

            if (damageable == null)
            {
                Debug.LogWarning("[SurviveStatusManager] : 정신력이 0 이 되었지만 플레이어가 연결되지 않아 즉사시키지 못했습니다.");
                return;
            }

            Vector3 position = buffContainer.Owner != null ? buffContainer.Owner.transform.position : Vector3.zero;

#if UNITY_EDITOR
            Debug.Log("[SurviveStatusManager] : 정신력이 0 이 되어 플레이어를 즉사시킵니다.");
#endif

            damageable.TakeDamage(new CommonStruct.AttackInfo { Position = position, Damage = LethalDamage });
        }
        #endregion

        #region Temperature
        /// <summary>
        /// 온도 환경을 바꿉니다. (예: 용암지대에 들어가면 Hot, 빙하지대는 Cold, 나오면 Normal) 같은 값이면 아무 일도 없습니다.
        /// 환경이 바뀌면 변화 진행도를 0 부터 다시 세고 OnTemperatureEnvironmentChanged 를 발생시킵니다.
        /// </summary>
        [Button("테스트: 온도 환경 바꾸기")]
        public void SetTemperatureEnvironment(TemperatureEnvironment environment)
        {
            if (environment == CurrentTemperatureEnvironment)
            {
                return;
            }

            TemperatureEnvironment previous = CurrentTemperatureEnvironment;
            CurrentTemperatureEnvironment = environment;
            _temperatureProgress = 0f;

#if UNITY_EDITOR
            Debug.Log($"[SurviveStatusManager] : 온도 환경이 {previous} -> {environment} 로 바뀌었습니다.");
#endif

            OnTemperatureEnvironmentChanged?.Invoke(previous, environment);
        }

        /// <summary>
        /// 온도 환경에 따라 체온을 시간에 맞춰 바꿉니다. 차분함/열혈로 좁아진 체온 범위(MinLimit, MaxLimit)를 넘지 않습니다.
        /// - Hot : 체온이 계속 올라갑니다. 상한에 닿으면 멈춥니다.
        /// - Cold : 체온이 계속 내려갑니다. 하한에 닿으면 멈춥니다.
        /// - Normal : 체온이 복귀 목표(기본 50%)로 올라오거나 내려와서 목표에 닿으면 멈춥니다.
        /// </summary>
        private void TickTemperature(float deltaTime)
        {
            int current = bodyTemperatureManager.bodyTemperature;
            int min = bodyTemperatureManager.MinLimit;
            int max = bodyTemperatureManager.MaxLimit;

            switch (CurrentTemperatureEnvironment)
            {
                case TemperatureEnvironment.Hot:
                    if (current >= max)
                    {
                        _temperatureProgress = 0f;
                        return;
                    }

                    TickTemperatureChange(deltaTime, _hotRiseIntervalSeconds, _hotRiseAmount, max);
                    break;

                case TemperatureEnvironment.Cold:
                    if (current <= min)
                    {
                        _temperatureProgress = 0f;
                        return;
                    }

                    TickTemperatureChange(deltaTime, _coldFallIntervalSeconds, -_coldFallAmount, min);
                    break;

                default:
                    // 복귀 목표가 체온 범위 밖이면(예: 차분함 상한 69 인데 목표 80) 범위 안으로 맞춥니다.
                    int target = Mathf.Clamp(_normalRecoverTarget, min, max);

                    if (current == target)
                    {
                        _temperatureProgress = 0f;
                        return;
                    }

                    int recover = current < target ? _normalRecoverAmount : -_normalRecoverAmount;
                    TickTemperatureChange(deltaTime, _normalRecoverIntervalSeconds, recover, target);
                    break;
            }
        }

        /// <summary>
        /// 진행도를 쌓고, 간격에 도달할 때마다 signedAmount 만큼 limit 을 넘지 않게 바꿉니다. (양수는 limit 까지 올리고, 음수는 limit 까지 내림)
        /// </summary>
        private void TickTemperatureChange(float deltaTime, float intervalSeconds, int signedAmount, int limit)
        {
            if (signedAmount == 0)
            {
                _temperatureProgress = 0f;
                return;
            }

            float interval = Mathf.Max(0.1f, intervalSeconds);
            _temperatureProgress += deltaTime;

            while (_temperatureProgress >= interval)
            {
                _temperatureProgress -= interval;

                int current = bodyTemperatureManager.bodyTemperature;
                int room = signedAmount > 0 ? limit - current : current - limit;

                // 이미 limit 에 닿았으면 더 바꾸지 않습니다.
                if (room <= 0)
                {
                    _temperatureProgress = 0f;
                    break;
                }

                int step = Mathf.Min(Mathf.Abs(signedAmount), room);
                ChangeStatus(SurviveStatusType.BodyTemperature, signedAmount > 0 ? step : -step);
            }
        }
        #endregion

        #region Decay
        // ==================== 자연 감소 ============================

        /// <summary>
        /// 감소 진행도를 쌓고, 간격에 도달할 때마다 한 번씩 감소시킵니다. (프레임이 길어도 놓치지 않도록 반복)
        /// 간격과 감소량은 수분, 포만감이 각자 가지고 있어서 호출하는 쪽에서 넘깁니다.
        /// </summary>
        private void TickDecay(ref float progress, float rate, float deltaTime, float intervalSeconds, int amount, SurviveStatusType type)
        {
            float interval = Mathf.Max(0.1f, intervalSeconds);
            progress += deltaTime * rate;

            while (progress >= interval)
            {
                progress -= interval;
                ChangeStatus(type, -amount);
            }
        }

        /// <summary>
        /// 더위/추위 디버프가 걸려 있는지 다시 확인합니다. (더위 : 수분 감소 가속, 추위 : 포만감 감소 가속)
        /// </summary>
        private void RefreshDecayRates()
        {
            _isHot = HasEffect(HotDebuffId);
            _isCold = HasEffect(ColdDebuffId);
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
        #endregion

        #region Reset
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
        #endregion

        #region EffectSync
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
        #endregion

        #region Periodic
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
        #endregion

        #region StatusChange
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
        #endregion
    }
}