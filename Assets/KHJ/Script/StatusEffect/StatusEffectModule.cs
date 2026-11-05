using System;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 수치 보정 대상 스탯을 정의하는 열거형입니다. 이동속도, 공격력, 방어력, 시야 순서입니다.
    /// </summary>
    public enum StatType
    {
        None,
        MoveSpeed,
        AttackPower,
        Defense,
        Vision,
    }

    /// <summary>
    /// 상태 효과가 실제로 하는 일을 나타내는 모듈의 부모 클래스입니다.
    /// 상태 효과 SO 는 이 모듈들을 조합해서 효과를 표현합니다. (디버프마다 클래스를 만들지 않기 위함)
    /// </summary>
    [Serializable]
    public abstract class StatusEffectModule
    {
    }

    /// <summary>
    /// 스탯을 퍼센트(정수)로 보정하는 모듈입니다. 여러 보정은 곱연산으로 적용합니다.
    /// 예) +40 -> x1.4 / -30 -> x0.7
    /// </summary>
    [Serializable]
    public class StatModifierModule : StatusEffectModule
    {
        [SerializeField, LabelText("대상 스탯")]
        private StatType _stat = StatType.None;
        public StatType stat => _stat;

        [SerializeField, LabelText("증감 (%)"), MinValue(-100)]
        private int _percent = 0;
        public int percent => _percent;

        /// <summary>
        /// 곱연산에 사용하는 배율입니다. 예) +40% -> 1.4 / -30% -> 0.7
        /// </summary>
        public float multiplier => 1f + _percent / 100f;
    }

    /// <summary>
    /// 체력 증감 종류를 정의하는 열거형입니다. Damage 는 대미지, Heal 은 회복입니다.
    /// </summary>
    public enum HealthChangeType
    {
        None,
        Damage,
        Heal,
    }

    /// <summary>
    /// 체력을 최대 체력 기준 퍼센트(정수)로 깎거나 회복하는 모듈입니다.
    /// 대미지는 IDamageable, 회복은 IHealable 을 통해 대상에게 전달합니다.
    /// 반복 간격이 0 이면 한 번만, 0보다 크면 그 간격(초)마다 반복합니다. (반복 호출은 상태 효과를 관리하는 쪽의 몫)
    /// 예) 탈수 : Damage 10% / 1초마다, 회복 포션 : Heal 25% / 한 번
    /// </summary>
    [Serializable]
    public class HealthChangeModule : StatusEffectModule
    {
        [SerializeField, LabelText("체력 증감 종류")]
        private HealthChangeType _changeType = HealthChangeType.None;
        public HealthChangeType changeType => _changeType;

        [SerializeField, LabelText("최대 체력 대비 (%)"), MinValue(0)]
        private int _percent = 0;
        public int percent => _percent;

        [SerializeField, LabelText("반복 간격 (초, 0 = 한 번)"), MinValue(0)]
        private float _intervalSeconds = 0f;
        public float intervalSeconds => _intervalSeconds;

        /// <summary>
        /// 최대 체력을 기준으로 한 번에 증감할 양을 계산합니다.
        /// </summary>
        public int CalculateAmount(int maxHealth) => Mathf.RoundToInt(maxHealth * _percent / 100f);

        /// <summary>
        /// 대상에게 한 번(한 틱)만큼 적용합니다. 대미지는 IDamageable, 회복은 IHealable 로 전달합니다.
        /// </summary>
        public void Apply(IDamageable target, int maxHealth, Vector3 position = default)
        {
            int amount = CalculateAmount(maxHealth);
            if (amount <= 0)
            {
                return;
            }

            switch (_changeType)
            {
                case HealthChangeType.Damage:
                    target.TakeDamage(new CommonStruct.AttackInfo { Position = position, Damage = amount });
                    break;
                case HealthChangeType.Heal:
                    if (target is Enzeun.Runtime.IHealable healable)
                    {
                        healable.Heal(amount);
                    }
                    else
                    {
                        Debug.LogWarning("[HealthChangeModule]: 대상이 IHealable 을 구현하지 않아 회복할 수 없습니다.");
                    }
                    break;
            }
        }
    }
}
