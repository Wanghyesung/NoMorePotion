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
    }
}
