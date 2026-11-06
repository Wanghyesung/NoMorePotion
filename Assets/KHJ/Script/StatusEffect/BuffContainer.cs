using System;
using System.Collections.Generic;
using Enzeun.SO;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 대상(플레이어 등)에게 런타임에 걸려 있는 버프/디버프 목록을 가지고 있는 컨테이너입니다.
    /// - 버프와 디버프는 각각의 목록으로 따로 보관합니다. (StatusEffectData 의 종류로 구분)
    /// - 스탯 보정은 곱연산입니다. Unique/RefreshDuration 효과는 같은 효과가 여러 건이어도 가장 강한 한 건만, Stackable 효과는 전부 반영합니다.
    /// - 같은 효과도 건 주체별로 따로 보관하므로, 주체 단위로 제거할 수 있습니다.
    /// - Owner : 이 컨테이너의 주인입니다. 몬스터는 자기 자신이 기본 Owner 이고, 플레이어는 SetOwner 로 연결합니다.
    /// - 중첩 정책은 StackPolicy 참고, 틱은 ProcessTickModules/OnPeriodicAction 참고.
    /// - 제작 실패 확률도 GetCraftFailChance 로 조회할 수 있습니다.
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
        // 스탯 종류 개수는 한 번만 구합니다. (인스턴스마다 Enum.GetValues 를 호출하면 GC 할당 발생)
        private static readonly int StatTypeCount = Enum.GetValues(typeof(StatType)).Length;

        /// <summary>
        /// 버프 전체를 곱한 배율이 늘어날 수 있는 최대치(%)입니다. (일괄 적용)
        /// </summary>
        public const int MaxBuffPercent = 70;

        /// <summary>
        /// 디버프 전체를 곱한 배율이 줄어들 수 있는 최대치(%)입니다. (일괄 적용)
        /// </summary>
        public const int MaxDebuffPercent = 80;

        private readonly float[] _statMultipliers = new float[StatTypeCount];
        private readonly float[] _buffMultipliers = new float[StatTypeCount];
        private readonly float[] _debuffMultipliers = new float[StatTypeCount];
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

        /// <summary>
        /// 목록에 지속시간이 있거나 틱 모듈이 있는 효과가 하나라도 있으면 true 입니다. (Update 가 필요한지 판단)
        /// </summary>
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
        /// 처리 순서는 만료 제거 -> 틱 모듈 처리이며, 만료된 건은 틱 없이 바로 제거합니다.
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
        /// <remarks>
        /// 중첩 정책별 동작
        /// - Unique : 같은 효과를 같은 주체가 이미 걸었으면 무시합니다.
        /// - RefreshDuration : 지속시간만 갱신합니다. 새로 넘긴 percentOverride 는 무시되고 기존 건의 값을 유지합니다.
        /// - Stackable : FindSame 을 쓰지 않으므로 같은 주체가 반복해서 Apply 해도 매번 새 건이 추가됩니다. (지속시간은 각자, 스탯은 전부 곱연산)
        /// </remarks>
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

                case StackPolicy.Stackable:
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
        /// 포션을 제작할 때 실패해서 함정 포션이 될 확률(%)입니다. 걸려 있는 상태 효과의 "제조 실패 확률" 중 가장 높은 값이며,
        /// 해당 효과가 없으면 0 입니다. (예: 똑똑이 0%, 멍청함 50%)
        /// </summary>
        public int GetCraftFailChance()
        {
            return Mathf.Max(GetMaxCraftFailChance(_buffs), GetMaxCraftFailChance(_debuffs));
        }

        private static int GetMaxCraftFailChance(List<ActiveStatusEffect> list)
        {
            int max = 0;

            for (int i = 0; i < list.Count; i++)
            {
                IReadOnlyList<StatusEffectModule> modules = list[i].data.modules;

                for (int m = 0; m < modules.Count; m++)
                {
                    if (modules[m] is CraftFailChanceModule failChance && failChance.FailPercent > max)
                    {
                        max = failChance.FailPercent;
                    }
                }
            }

            return max;
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
        /// 버프끼리 곱한 값은 최대 +70%(1.7), 디버프끼리 곱한 값은 최대 -80%(0.2)로 제한한 뒤 둘을 곱합니다.
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

        /// <summary>
        /// 상태 효과 종류에 맞는 목록(버프/디버프)을 반환합니다. 종류가 설정되지 않았으면 null 입니다.
        /// </summary>
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

        /// <summary>
        /// 목록에서 같은 효과를 같은 주체가 건 건을 찾습니다. 없으면 null 입니다.
        /// </summary>
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

        /// <summary>
        /// 버프/디버프 전체를 다시 훑어 스탯별 배율 캐시를 1 부터 새로 계산합니다.
        /// 버프끼리, 디버프끼리 각각 곱한 뒤 상한(MaxBuffPercent, MaxDebuffPercent)을 적용하고 둘을 곱합니다.
        /// </summary>
        private void RecalculateStatMultipliers()
        {
            for (int i = 0; i < _statMultipliers.Length; i++)
            {
                _buffMultipliers[i] = 1f;
                _debuffMultipliers[i] = 1f;
            }

            AccumulateStatMultipliers(_buffs, _buffMultipliers);
            AccumulateStatMultipliers(_debuffs, _debuffMultipliers);

            float buffLimit = 1f + MaxBuffPercent / 100f;
            float debuffLimit = 1f - MaxDebuffPercent / 100f;

            for (int i = 0; i < _statMultipliers.Length; i++)
            {
                _statMultipliers[i] = Mathf.Min(_buffMultipliers[i], buffLimit) * Mathf.Max(_debuffMultipliers[i], debuffLimit);
            }

            _isStatMultiplierDirty = false;
        }

        // 목록의 스탯 보정을 multipliers 에 곱해 넣습니다. (상한은 호출한 쪽에서 적용)
        private void AccumulateStatMultipliers(List<ActiveStatusEffect> list, float[] multipliers)
        {
            for (int i = 0; i < list.Count; i++)
            {
                ActiveStatusEffect effect = list[i];

                // Stackable 은 전부 곱연산하고, 그 외에는 같은 상태 효과가 여러 건이어도 수치가 가장 큰 한 건만 반영합니다.
                if (effect.data.stackPolicy != StackPolicy.Stackable && !IsStrongest(list, i))
                {
                    continue;
                }

                IReadOnlyList<StatusEffectModule> modules = effect.data.modules;

                for (int m = 0; m < modules.Count; m++)
                {
                    if (modules[m] is StatModifierModule statModule && statModule.stat != StatType.None)
                    {
                        int percent = effect.percentOverride != 0 ? effect.percentOverride : statModule.percent;
                        percent = Mathf.Max(percent, -100);   // 배율이 음수가 되지 않도록 하한 고정

                        multipliers[(int)statModule.stat] *= 1f + percent / 100f;
                    }
                }
            }
        }

        /// <summary>
        /// 같은 상태 효과 중에서 이 건이 가장 강한지 확인합니다. 수치가 같으면 먼저 걸린 것이 이깁니다.
        /// 비교 기준은 실제로 적용되는 수치(GetStrength)입니다. 스탯 보정이 없는 효과(체력 증감 등)는 모두 0 이라 먼저 걸린 건이 이깁니다.
        /// 스탯은 Stackable 이 아닐 때만, 틱(체력 증감, 주기 행동)은 정책과 상관없이 한 건만 발동하도록 이 판정을 씁니다.
        /// </summary>
        private bool IsStrongest(List<ActiveStatusEffect> list, int index)
        {
            ActiveStatusEffect target = list[index];
            int targetStrength = GetStrength(target);

            for (int i = 0; i < list.Count; i++)
            {
                if (i == index || list[i].data != target.data)
                {
                    continue;
                }

                int otherStrength = GetStrength(list[i]);

                if (i < index ? otherStrength >= targetStrength : otherStrength > targetStrength)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 효과의 실제 적용 수치(%)의 절댓값입니다. 넘겨받은 수치(percentOverride)가 있으면 그것을, 0 이면 SO 의 스탯 보정 수치를 씁니다.
        /// 스탯 보정 모듈이 없으면 0 입니다.
        /// </summary>
        private static int GetStrength(ActiveStatusEffect effect)
        {
            if (effect.percentOverride != 0)
            {
                return Mathf.Abs(effect.percentOverride);
            }

            int strength = 0;
            IReadOnlyList<StatusEffectModule> modules = effect.data.modules;

            for (int m = 0; m < modules.Count; m++)
            {
                if (modules[m] is StatModifierModule statModule && statModule.stat != StatType.None)
                {
                    strength = Mathf.Max(strength, Mathf.Abs(statModule.percent));
                }
            }

            return strength;
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
