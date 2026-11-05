using System;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 체력을 증감하는 효과입니다. 기존 HealthChangeModule 을 그대로 사용합니다. (대미지는 IDamageable, 회복은 IHealable)
    /// </summary>
    [Serializable]
    public class HealthChangePotionEffect : PotionEffect
    {
        [SerializeField, LabelText("체력 증감")]
        private HealthChangeModule _healthChange = new HealthChangeModule();
        public HealthChangeModule healthChange => _healthChange;

        // 초당 대미지처럼 일정 시간 동안 반복하는 효과의 지속시간입니다. 반복 간격은 모듈의 "반복 간격"을 사용합니다.
        [SerializeField, LabelText("지속시간 (초, 0 = 즉시 한 번)"), MinValue(0)]
        private float _durationSeconds = 0f;
        public float durationSeconds => _durationSeconds;
    }
}
