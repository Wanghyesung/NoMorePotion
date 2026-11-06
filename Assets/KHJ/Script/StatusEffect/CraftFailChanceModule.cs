using System;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 포션 제작이 실패해서 함정 포션이 될 확률(%)을 정의하는 모듈입니다.
    /// 예) 똑똑이 : 0% / 멍청함 : 50%
    /// 이 모듈을 가진 상태 효과가 여러 개 걸려 있으면 그중 가장 높은 확률을 사용합니다. (BuffContainer.GetCraftFailChance)
    /// </summary>
    [Serializable]
    public class CraftFailChanceModule : StatusEffectModule
    {
        [SerializeField, LabelText("제조 실패 확률 (%)"), Range(0, 100)]
        private int _failPercent = 0;
        public int FailPercent => _failPercent;
    }
}
