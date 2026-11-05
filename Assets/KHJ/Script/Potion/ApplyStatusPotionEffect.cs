using System;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 버프/디버프를 적용하는 효과입니다. 상태 효과 SO 를 직접 참조하며, 수치와 지속시간은 적용 시점에 전달합니다.
    /// 예) 빠른발 +30% 60초 (StatModifierModule 의 percent 는 0 으로 두고 여기서 값을 넘김)
    /// </summary>
    [Serializable]
    public class ApplyStatusPotionEffect : PotionEffect
    {
        [SerializeField, LabelText("상태 효과"), Required]
        private StatusEffectData _statusEffect = null;
        public StatusEffectData statusEffect => _statusEffect;

        [SerializeField, LabelText("수치 (%) (0 = SO 값 사용)")]
        private int _percent = 0;
        public int percent => _percent;

        [SerializeField, LabelText("지속시간 (초, 0 = 지속시간 없음)"), MinValue(0)]
        private float _durationSeconds = 0f;
        public float durationSeconds => _durationSeconds;
    }
}
