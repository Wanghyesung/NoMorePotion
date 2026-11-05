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
    /// 이 클래스는 목록 관리, 중첩, 지속시간, 스탯 배율 조회까지만 담당합니다.
    /// 체력 증감처럼 주기적으로 실행하는 효과(탈수의 초당 대미지 등)는 이 목록을 읽는 별도 실행기의 몫입니다.
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

        // 스탯 배율 캐시입니다. 목록이 바뀔 때만 다시 계산합니다.
        private readonly float[] _statMultipliers = new float[Enum.GetValues(typeof(StatType)).Length];
        private bool _isStatMultiplierDirty = true;


        private void Update()
        {
            float deltaTime = Time.deltaTime;
            TickAndRemoveExpired(_buffs, deltaTime);
            TickAndRemoveExpired(_debuffs, deltaTime);
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

                list[i].Tick(deltaTime);

                if (list[i].isExpired)
                {
                    RemoveAt(list, i);
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
