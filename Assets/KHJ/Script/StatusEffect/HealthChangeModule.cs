using System;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
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
    /// 체력을 고정 수치(정수)만큼 깎거나 회복하는 모듈입니다. (플레이어 기본 체력 3000 기준)
    /// 대미지는 IDamageable, 회복은 IHealable 을 통해 대상에게 전달합니다.
    /// 반복 간격이 0 이면 한 번만, 0보다 크면 그 간격(초)마다 반복합니다. (반복 호출은 상태 효과를 관리하는 쪽의 몫)
    /// 예) 탈수 : Damage 300 / 1초마다, 회복 포션 : Heal 750 / 한 번
    /// </summary>
    [Serializable]
    public class HealthChangeModule : StatusEffectModule
    {
        [SerializeField, LabelText("체력 증감 종류")]
        private HealthChangeType _changeType = HealthChangeType.None;
        public HealthChangeType changeType => _changeType;

        // None = 고정 대미지(공격자 없이 전달), Normal = 무속성 대미지, 그 외는 해당 속성 대미지입니다.
        [SerializeField, LabelText("대미지 속성 (None = 고정 대미지)"), ShowIf("_changeType", HealthChangeType.Damage)]
        private DamageType _damageType = DamageType.Normal;
        public DamageType damageType => _damageType;

        [SerializeField, LabelText("증감 수치"), MinValue(0)]
        private int _amount = 0;
        public int amount => _amount;

        [SerializeField, LabelText("반복 간격 (초, 0 = 한 번)"), MinValue(0)]
        private float _intervalSeconds = 0f;
        public float intervalSeconds => _intervalSeconds;

        /// <summary>
        /// 대미지 속성을 IAttacker 로 전달하기 위한 어댑터입니다. (AttackInfo 에는 속성 필드가 없음)
        /// 속성은 모듈 설정값이라 한 번 만들어 재사용합니다. (틱마다 할당하지 않기 위함)
        /// </summary>
        private sealed class ElementAttacker : IAttacker
        {
            public DamageType DamageType { get; set; }
        }

        [NonSerialized]
        private ElementAttacker _elementAttacker;

        /// <summary>
        /// 대상에게 한 번(한 틱)만큼 적용합니다. 대미지는 IDamageable, 회복은 IHealable 로 전달합니다.
        /// </summary>
        public void Apply(IDamageable target, Vector3 position = default)
        {
            if (_amount <= 0)
            {
                return;
            }

            switch (_changeType)
            {
                case HealthChangeType.Damage:
                    var attackInfo = new CommonStruct.AttackInfo { Position = position, Damage = _amount };

                    // 속성이 None 이면 고정 대미지입니다. 공격자(IAttacker) 없이 TakeDamage(AttackInfo) 로 전달합니다.
                    if (_damageType == DamageType.None)
                    {
                        target.TakeDamage(attackInfo);
                        break;
                    }

                    if (_elementAttacker == null)
                    {
                        _elementAttacker = new ElementAttacker { DamageType = _damageType };
                    }
                    target.TakeDamage(attackInfo, _elementAttacker);
                    break;

                case HealthChangeType.Heal:
                    if (target is Enzeun.Runtime.IHealable healable)
                    {
                        healable.Heal(_amount);
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
