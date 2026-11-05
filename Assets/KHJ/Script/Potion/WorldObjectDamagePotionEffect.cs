using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 월드 오브젝트(식물, 광물, 뼈 등)에 대미지를 주는 채집용 포션 효과입니다.
    /// 지정한 종류의 월드 오브젝트에만 대미지가 들어갑니다.
    /// 예) 서걱서걱 포션 : 식물 대미지 1 / 깡깡 포션 : 광물, 뼈 대미지 1
    /// </summary>
    [Serializable]
    public class WorldObjectDamagePotionEffect : PotionEffect
    {
        [SerializeField, LabelText("대상 종류")]
        private List<WorldObjectType> _targetTypes = new List<WorldObjectType>();
        public IReadOnlyList<WorldObjectType> targetTypes => _targetTypes;

        [SerializeField, LabelText("대미지"), MinValue(0)]
        private int _damage = 0;
        public int damage => _damage;
    }
}
