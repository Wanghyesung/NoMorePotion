using System;
using System.Collections.Generic;
using Enzeun.SO;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 대상(플레이어 등)에게 런타임에 걸려 있는 버프/디버프 목록을 가지고 있는 컨테이너입니다.
    ///
    /// 규칙
    /// - 버프와 디버프는 각각의 목록으로 따로 보관합니다. (StatusEffectData 의 종류로 구분)
    /// - 서로 다른 상태 효과는 전부 적용됩니다. (예: 혼란, 죽어라 동시 적용)
    /// - 같은 상태 효과가 다시 걸릴 때는 SO 의 중첩 정책을 따릅니다.
    ///   Unique : 같은 주체가 건 것은 무시 / RefreshDuration : 같은 주체가 다시 걸면 지속시간 갱신 /
    ///   StrongestWins : 별도로 보관하되 스탯에는 수치가 가장 큰 것 하나만 적용 (지속시간은 각자)
    /// - 같은 효과를 다른 주체가 걸면 각자 보관합니다. 그래서 한 주체가 제거해도 다른 주체가 건 것은 남습니다.
    /// - 스탯 보정은 곱연산입니다. 효과 하나(상태 효과 ID 기준)당 한 번만 반영합니다.
    ///
    /// - 체력 증감 모듈(탈수의 초당 대미지 등)은 걸리자마자 한 번, 이후 반복 간격마다 Owner 의 IDamageable 에게 적용합니다.
    ///   같은 효과가 여러 건이어도 대미지는 한 건만 주고, 프레임이 튀어도 한 번의 갱신에 틱은 최대 한 번입니다.
    ///   멀티플레이에서는 권한(HasAuthority)이 있는 클라이언트에서만 대미지를 줍니다.
    /// - 주기 행동 모듈(추위의 이동 정지, 허기의 아이템 소모 등)은 같은 방식으로 발동 시점만 관리하고, OnPeriodicAction 이벤트로 알립니다.
    ///   실제 행동은 이벤트를 받는 쪽이 처리하므로, 컨테이너는 플레이어나 인벤토리를 알지 못합니다.
    /// - 지속시간이 있거나 틱 모듈이 있는 효과가 걸려 있을 때만 Update 를 돌립니다.
    ///
    /// - Owner : 이 컨테이너의 주인입니다. 몬스터는 자기 자신이 기본 Owner 이고, 플레이어는 SetOwner 로 연결합니다.
    ///
    /// 이 클래스는 목록 관리, 중첩, 지속시간, 스탯 배율 조회, 체력 증감 틱까지 담당합니다.
    /// 행동 변화 효과(갈증, 허기 등)는 Has() 로 걸려 있는지 확인해서 각 시스템이 처리합니다.
    /// </summary>
    public class BuffContainer : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, LabelText("버프 목록"), PropertyOrder(-3)]
        private List<ActiveStatusEffect> _buffs = new List<ActiveStatusEffect>();
        public IReadOnlyList<ActiveStatusEffect> buffs => _buffs;

        [ShowInInspector, ReadOnly, LabelText("디버프 목록"), PropertyOrder(-2)]
        private List<ActiveStatusEffect> _debuffs = new List<ActiveStatusEffect>();
        public IReadOnlyList<ActiveStatusEffect> debuffs => _debuffs;

        /// <summary>
        /// 상태 효과가 목록에 새로 추가되었을 때 발생합니다. (지속시간 갱신, 무시된 경우에는 발생하지 않음)
        /// </summary>
        public event Action<ActiveStatusEffect> OnEffectAdded;

        /// <summary>
        /// 상태 효과가 목록에서 제거되었을 때 발생합니다. (만료, 주체 제거, 전체 제거 모두 포함)
        /// </summary>
        public event Action<ActiveStatusEffect> OnEffectRemoved;

        /// <summary>
        /// 주기 행동(이동 정지, 아이템 소모 등)이 발동할 때 발생합니다. 걸리자마자 한 번, 이후 반복 간격마다 발생합니다.
        /// 컨테이너는 발동 시점만 관리하고, 실제 행동은 이 이벤트를 받는 쪽이 처리합니다.
        /// 같은 효과가 여러 건이어도 한 건만 발생하고, 권한(HasAuthority)이 있을 때만 발생합니다.
        /// </summary>
        public event Action<ActiveStatusEffect, PeriodicActionModule> OnPeriodicAction;

        // 스탯 배율 캐시입니다. 목록이 바뀔 때만 다시 계산합니다.
        // 스탯 종류 개수는 한 번만 구합니다. (인스턴스마다 Enum.GetValues 배열을 만들어 버리면 컨테이너 생성 때마다 GC 할당이 생김)
        private static readonly int StatTypeCount = Enum.GetValues(typeof(StatType)).Length;

        private readonly float[] _statMultipliers = new float[StatTypeCount];
        private bool _isStatMultiplierDirty = true;


        // ==================== owner (이 컨테이너의 주인) ============================

        // 몬스터처럼 컨테이너가 주인과 같은 오브젝트에 붙는 경우 켭니다. 플레이어 컨테이너(생존 상태 매니저 쪽)는 끄고 SetOwner 로 연결합니다.
        [SerializeField, LabelText("Awake 에서 자기 자신을 Owner 로 지정")]
        private bool _assignOwnerOnAwake = true;

        /// <summary>
        /// 이 컨테이너의 주인 오브젝트(루트)입니다. 폭발 등에서 맞은 콜라이더로 컨테이너를 찾을 때 기준이 됩니다.
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("Owner"), PropertyOrder(-4)]
        public GameObject Owner { get; private set; }

        /// <summary>
        /// 체력 증감(초당 대미지 등)을 받을 대상입니다. Owner 를 지정할 때 한 번만 찾습니다.
        /// </summary>
        public IDamageable Damageable { get; private set; }

        /// <summary>
        /// 이 클라이언트가 이 컨테이너의 체력 증감을 실행할 권한이 있는지 여부입니다. 확인 함수를 지정하지 않으면 항상 true 입니다.
        /// (멀티플레이에서 플레이어는 본인 클라이언트, 몬스터는 마스터 클라이언트만 대미지를 줘서 중복 대미지를 막기 위함)
        /// </summary>
        public bool HasAuthority => _authorityCheck == null || _authorityCheck();

        private Func<bool> _authorityCheck;

        // Owner 오브젝트로 컨테이너를 찾기 위한 등록부입니다.
        private static readonly Dictionary<GameObject, BuffContainer> Containers = new Dictionary<GameObject, BuffContainer>();

        // 등록할 때 쓴 키입니다. 주인이 먼저 파괴되어도 같은 키로 등록을 해제하기 위해 따로 들고 있습니다.
        private GameObject _registeredKey;

        private void Awake()
        {
            if (_assignOwnerOnAwake)
            {
                SetOwner(gameObject);
            }

            // 시간이 흘러야 하는 효과가 없으면 Update 를 돌리지 않습니다. (몬스터가 많을 때 Update 호출 비용을 없애기 위함)
            UpdateEnabledState();
        }

        /// <summary>
        /// 시간이 흘러야 하는 효과(지속시간이 있거나 체력 증감 틱이 있는 효과)가 있을 때만 컴포넌트를 켭니다.
        /// 스탯 보정이나 행동만 있는 영구 효과(더위, 갈증 등)는 시간이 흐를 일이 없으므로 Update 를 돌리지 않습니다.
        /// </summary>
        private void UpdateEnabledState()
        {
            bool needsTick = NeedsTick(_buffs) || NeedsTick(_debuffs);

            if (enabled != needsTick)
            {
                enabled = needsTick;
            }
        }

        private static bool NeedsTick(List<ActiveStatusEffect> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].hasDuration || list[i].HasTickModule)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            Unregister();
        }

        /// <summary>
        /// 이 컨테이너의 주인을 지정합니다. 주인(또는 그 부모/자식)에서 IDamageable 을 찾아 체력 증감 대상으로 삼습니다.
        /// 플레이어는 생성된 뒤에 호출해서 연결합니다.
        /// </summary>
        public void SetOwner(GameObject owner)
        {
            Unregister();

            Owner = owner;
            Damageable = null;

            if (owner == null)
            {
                return;
            }

            Damageable = owner.GetComponentInParent<IDamageable>() ?? owner.GetComponentInChildren<IDamageable>();

            if (Damageable == null)
            {
                Debug.LogWarning($"[BuffContainer]: Owner {owner.name} 에서 IDamageable 을 찾지 못해 체력 증감 효과를 적용할 수 없습니다.");
            }

            if (Containers.TryGetValue(owner, out BuffContainer existing) && existing != this)
            {
                Debug.LogWarning($"[BuffContainer]: Owner {owner.name} 에 이미 다른 BuffContainer 가 등록되어 있어 새 컨테이너로 교체합니다.");
            }

            Containers[owner] = this;
            _registeredKey = owner;
        }

        /// <summary>
        /// 이 컨테이너가 체력 증감을 실행할 권한이 있는지 확인하는 함수를 지정합니다.
        /// 예) 플레이어 : () => photonView.IsMine / 몬스터 : () => PhotonNetwork.IsMasterClient
        /// </summary>
        public void SetAuthorityCheck(Func<bool> authorityCheck)
        {
            _authorityCheck = authorityCheck;
        }

        /// <summary>
        /// start 에서 부모 방향으로 올라가며 Owner 로 등록된 오브젝트를 찾아 그 컨테이너를 반환합니다.
        /// 컨테이너가 Owner 와 다른 오브젝트에 붙어 있어도 찾을 수 있습니다. 없으면 null 입니다.
        /// </summary>
        public static BuffContainer FindByOwner(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                if (Containers.TryGetValue(current.gameObject, out BuffContainer container))
                {
                    return container;
                }
            }

            return null;
        }

        private void Unregister()
        {
            if (!ReferenceEquals(_registeredKey, null)
                && Containers.TryGetValue(_registeredKey, out BuffContainer registered)
                && registered == this)
            {
                Containers.Remove(_registeredKey);
            }

            _registeredKey = null;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 지속시간을 줄여 만료된 효과를 제거하고, 남은 효과의 체력 증감(초당 대미지 등)을 틱 시점에 적용합니다.
        /// </summary>
        private void Tick(float deltaTime)
        {
            TickAndRemoveExpired(_buffs, deltaTime);
            TickAndRemoveExpired(_debuffs, deltaTime);
        }

        /// <summary>
        /// 체력 증감을 적용할 수 있는 대상인지 확인합니다. (지정되지 않았거나 이미 파괴된 대상은 제외)
        /// </summary>
        private bool CanApplyHealthChange()
        {
            if (Damageable == null || !HasAuthority)
            {
                return false;
            }

            // IDamageable 구현체가 파괴된 MonoBehaviour 인 경우를 걸러냅니다.
            if (Damageable is UnityEngine.Object unityObject && unityObject == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 상태 효과를 적용합니다.
        /// </summary>
        /// <param name="data">적용할 상태 효과</param>
        /// <param name="source">건 주체. 주체 단위 제거에 사용합니다.</param>
        /// <param name="percentOverride">적용 시점에 넘기는 수치(%). 0 이면 SO 값을 사용합니다. 부호 그대로 사용합니다. (디버프는 음수)</param>
        /// <param name="duration">지속시간(초). 지속시간이 있는 효과는 0보다 커야 합니다.</param>
        /// <returns>목록에 반영되었으면 true (새로 추가 또는 지속시간 갱신), 무시되었으면 false</returns>
        public bool Apply(StatusEffectData data, object source, int percentOverride = 0, float duration = 0f)
        {
            if (data == null)
            {
                Debug.LogWarning("[BuffContainer]: 적용할 상태 효과가 null 입니다.");
                return false;
            }

            List<ActiveStatusEffect> list = GetList(data.statusEffectType);
            if (list == null)
            {
                Debug.LogWarning($"[BuffContainer]: 상태 효과 {data.statusEffectName}({data.ID})의 버프/디버프 종류가 설정되지 않았습니다.");
                return false;
            }

            if (data.hasDuration && duration <= 0f)
            {
                Debug.LogWarning($"[BuffContainer]: 지속시간이 있는 상태 효과 {data.statusEffectName}({data.ID})에 지속시간이 0 이하로 전달되어 적용하지 않았습니다.");
                return false;
            }

            StackPolicy policy = data.stackPolicy;
            if (policy == StackPolicy.None)
            {
                Debug.LogWarning($"[BuffContainer]: 상태 효과 {data.statusEffectName}({data.ID})의 중첩 정책이 설정되지 않아 Unique 로 처리합니다.");
                policy = StackPolicy.Unique;
            }

            ActiveStatusEffect same = FindSame(list, data, source);

            switch (policy)
            {
                case StackPolicy.Unique:
                    if (same != null)
                    {
                        return false;
                    }
                    break;

                case StackPolicy.RefreshDuration:
                    if (same != null)
                    {
                        same.RefreshDuration();
                        return true;
                    }
                    break;

                case StackPolicy.StrongestWins:
                    break;
            }

            var added = new ActiveStatusEffect(data, source, percentOverride, data.hasDuration ? duration : 0f);
            list.Add(added);
            _isStatMultiplierDirty = true;
            UpdateEnabledState();

#if UNITY_EDITOR
            Debug.Log($"[BuffContainer] : {data.statusEffectName}({data.ID}) 적용");
#endif

            OnEffectAdded?.Invoke(added);
            return true;
        }

        /// <summary>
        /// 특정 주체가 건 특정 상태 효과를 제거합니다.
        /// </summary>
        /// <returns>제거된 개수</returns>
        public int Remove(StatusEffectData data, object source)
        {
            if (data == null)
            {
                return 0;
            }

            List<ActiveStatusEffect> list = GetList(data.statusEffectType);
            if (list == null)
            {
                return 0;
            }

            int removedCount = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count)
                {
                    continue;
                }

                if (list[i].data == data && Equals(list[i].source, source))
                {
                    RemoveAt(list, i);
                    removedCount++;
                }
            }

            return removedCount;
        }

        /// <summary>
        /// 특정 주체가 건 상태 효과를 모두 제거합니다. (버프, 디버프 모두)
        /// 예) 체온 구간이 바뀔 때 이전 구간이 건 디버프만 한꺼번에 빼기
        /// </summary>
        /// <returns>제거된 개수</returns>
        public int RemoveBySource(object source)
        {
            return RemoveBySource(_buffs, source) + RemoveBySource(_debuffs, source);
        }

        /// <summary>
        /// 모든 상태 효과를 제거합니다.
        /// </summary>
        public void Clear()
        {
            ClearList(_buffs);
            ClearList(_debuffs);
        }

        /// <summary>
        /// 해당 상태 효과가 하나라도 걸려 있는지 확인합니다. (행동 변화만 있는 효과의 판정에 사용)
        /// </summary>
        public bool Has(StatusEffectData data)
        {
            if (data == null)
            {
                return false;
            }

            List<ActiveStatusEffect> list = GetList(data.statusEffectType);
            if (list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].data == data)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 해당 스탯에 적용되는 최종 배율을 반환합니다. 모든 보정을 곱연산한 값입니다. (보정이 없으면 1)
        /// 예) 이동속도 +40%(1.4) 와 -30%(0.7) 가 같이 걸려 있으면 0.98
        /// </summary>
        public float GetStatMultiplier(StatType stat)
        {
            if (_isStatMultiplierDirty)
            {
                RecalculateStatMultipliers();
            }

            return _statMultipliers[(int)stat];
        }

        // ==================== 내부 처리 ============================

        private List<ActiveStatusEffect> GetList(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Buff:
                    return _buffs;
                case StatusEffectType.Debuff:
                    return _debuffs;
                default:
                    return null;
            }
        }

        private ActiveStatusEffect FindSame(List<ActiveStatusEffect> list, StatusEffectData data, object source)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].data == data && Equals(list[i].source, source))
                {
                    return list[i];
                }
            }

            return null;
        }

        private void RemoveAt(List<ActiveStatusEffect> list, int index)
        {
            ActiveStatusEffect removed = list[index];
            list.RemoveAt(index);
            _isStatMultiplierDirty = true;

#if UNITY_EDITOR
            Debug.Log($"[BuffContainer] : {removed.data.statusEffectName}({removed.data.ID}) 제거");
#endif

            OnEffectRemoved?.Invoke(removed);

            // 이벤트 구독자가 효과를 다시 걸었을 수 있으므로, 이벤트 이후의 목록 상태로 판단합니다.
            UpdateEnabledState();
        }

        private int RemoveBySource(List<ActiveStatusEffect> list, object source)
        {
            int removedCount = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                // 이벤트 구독자가 목록을 바꿨을 수 있으므로 범위를 다시 확인합니다.
                if (i >= list.Count)
                {
                    continue;
                }

                if (Equals(list[i].source, source))
                {
                    RemoveAt(list, i);
                    removedCount++;
                }
            }

            return removedCount;
        }

        private void ClearList(List<ActiveStatusEffect> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count)
                {
                    continue;
                }

                RemoveAt(list, i);
            }
        }

        private void TickAndRemoveExpired(List<ActiveStatusEffect> list, float deltaTime)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count)
                {
                    continue;
                }

                ActiveStatusEffect effect = list[i];
                effect.Tick(deltaTime);

                if (effect.isExpired)
                {
                    RemoveAt(list, i);
                    continue;
                }

                if (effect.HasTickModule)
                {
                    ProcessTickModules(list, i, effect, deltaTime);
                }
            }
        }

        /// <summary>
        /// 효과가 가진 틱 모듈(체력 증감, 주기 행동)을 틱 시점에 맞춰 처리합니다.
        /// 같은 효과가 여러 건이어도 한 건(가장 강한 것, 같으면 먼저 걸린 것)만 발동합니다.
        /// 나머지 건도 타이머는 같이 흘려보내서, 앞선 건이 제거되어 순서가 넘어와도 한꺼번에 몰려 들어가지 않습니다.
        /// </summary>
        private void ProcessTickModules(List<ActiveStatusEffect> list, int index, ActiveStatusEffect effect, float deltaTime)
        {
            // 발동으로 목록이 바뀔 수 있으므로, 발동하기 전에 이 건이 담당인지 먼저 판정합니다.
            bool isActor = IsStrongest(list, index);
            IReadOnlyList<StatusEffectModule> modules = effect.data.modules;

            for (int m = 0; m < modules.Count; m++)
            {
                if (modules[m] is HealthChangeModule healthChange)
                {
                    bool isDue = effect.ConsumeTick(m, deltaTime, healthChange.intervalSeconds);

                    if (isDue && isActor && CanApplyHealthChange())
                    {
                        Vector3 position = Owner != null ? Owner.transform.position : transform.position;
                        healthChange.Apply(Damageable, position);
                    }
                }
                else if (modules[m] is PeriodicActionModule periodicAction)
                {
                    bool isDue = effect.ConsumeTick(m, deltaTime, periodicAction.IntervalSeconds);

                    // 행동 자체는 이벤트를 받는 쪽이 처리합니다. 컨테이너는 발동 시점만 관리합니다.
                    if (isDue && isActor && HasAuthority)
                    {
                        OnPeriodicAction?.Invoke(effect, periodicAction);
                    }
                }
            }
        }

        private void RecalculateStatMultipliers()
        {
            for (int i = 0; i < _statMultipliers.Length; i++)
            {
                _statMultipliers[i] = 1f;
            }

            AccumulateStatMultipliers(_buffs);
            AccumulateStatMultipliers(_debuffs);

            _isStatMultiplierDirty = false;
        }

        private void AccumulateStatMultipliers(List<ActiveStatusEffect> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                // 같은 상태 효과가 여러 건이면 수치가 가장 큰 한 건만 반영합니다.
                if (!IsStrongest(list, i))
                {
                    continue;
                }

                ActiveStatusEffect effect = list[i];
                IReadOnlyList<StatusEffectModule> modules = effect.data.modules;

                for (int m = 0; m < modules.Count; m++)
                {
                    if (modules[m] is StatModifierModule statModule && statModule.stat != StatType.None)
                    {
                        int percent = effect.percentOverride != 0 ? effect.percentOverride : statModule.percent;
                        percent = Mathf.Max(percent, -100);   // 배율이 음수가 되지 않도록 하한 고정

                        _statMultipliers[(int)statModule.stat] *= 1f + percent / 100f;
                    }
                }
            }
        }

        /// <summary>
        /// 같은 상태 효과 중에서 이 건이 가장 강한지 확인합니다. 수치가 같으면 먼저 걸린 것이 이깁니다.
        /// </summary>
        private bool IsStrongest(List<ActiveStatusEffect> list, int index)
        {
            ActiveStatusEffect target = list[index];
            int targetStrength = Mathf.Abs(target.percentOverride);

            for (int i = 0; i < list.Count; i++)
            {
                if (i == index || list[i].data != target.data)
                {
                    continue;
                }

                int otherStrength = Mathf.Abs(list[i].percentOverride);

                if (i < index ? otherStrength >= targetStrength : otherStrength > targetStrength)
                {
                    return false;
                }
            }

            return true;
        }

#if UNITY_EDITOR
        // ==================== 에디터 테스트용 ============================

        [ShowInInspector, ReadOnly, LabelText("스탯 배율 (이속/공격/방어/시야)"), PropertyOrder(-1)]
        private string statMultiplierSummary => Application.isPlaying
            ? $"{GetStatMultiplier(StatType.MoveSpeed):F2} / {GetStatMultiplier(StatType.AttackPower):F2} / {GetStatMultiplier(StatType.Defense):F2} / {GetStatMultiplier(StatType.Vision):F2}"
            : "Play 모드에서 확인";

        [Button("테스트: 상태 효과 적용", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void ApplyForTest(StatusEffectData data, int percentOverride = 0, float duration = 10f)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BuffContainer]: Play 모드에서만 테스트할 수 있습니다.");
                return;
            }

            Apply(data, this, percentOverride, duration);
        }

        [Button("테스트: 전체 제거", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void ClearForTest()
        {
            Clear();
        }
#endif
    }
}
