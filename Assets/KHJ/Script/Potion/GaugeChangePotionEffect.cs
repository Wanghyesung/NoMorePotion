using System;
using Enzeun.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 생존 상태를 최대치 기준 퍼센트(정수)로 증감하는 효과입니다.
    /// 예) 수분 +10 / 포만감 -10
    /// </summary>
    [Serializable]
    public class GaugeChangePotionEffect : PotionEffect
    {
        [SerializeField, LabelText("대상 생존 상태")]
        private SurviveStatusType _gauge = SurviveStatusType.None;
        public SurviveStatusType gauge => _gauge;

        [SerializeField, LabelText("증감 (% / 최대치 대비)"), MinValue(-100), MaxValue(100)]
        private int _percent = 0;
        public int percent => _percent;
    }
}
