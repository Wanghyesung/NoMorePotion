using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Enzeun.SO
{
    public enum PotionType
    {
        None,
        Status,
        Gathering,
        Battle,
    }

    public enum PotionExplosionRange
    {
        None,
        Small,
        Medium,
        Large,
    }

    public enum PotionCombinationType
    {
        None,
        Material,
        Potion,
    }

    [CreateAssetMenu(fileName = "PotionData", menuName = "Scriptable Objects/PotionData")]
    public class PotionData : ScriptableObject
    {
        [PreviewField(100, ObjectFieldAlignment.Left)]
        [LabelText("포션 아이콘")]
        [PropertyOrder(1)]
        [ShowInInspector]
        private Sprite _potionIcon = null;
        public Sprite potionIcon => _potionIcon;

        [LabelText("포션 ID"), BoxGroup("기본 정보")]
        [PropertyOrder(2)]
        [ShowInInspector]
        private int _potionID = 10001;
        public int potionID => _potionID;

        [LabelText("포션 이름"), BoxGroup("기본 정보")]
        [PropertyOrder(3)]
        [ShowInInspector]
        private string _potionName = "이름 입니다";
        public string potionName => _potionName;

        [LabelText("포션 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [PropertyOrder(4)]
        [ShowInInspector]
        private string _potionDesc = "설명 입니다";
        public string potionDesc => _potionDesc;

        [LabelText("포션 효과"), BoxGroup("기본 정보")]
        [TextArea]
        [PropertyOrder(5)]
        [ShowInInspector]
        private string _potionEffect = "효과 입니다";
        public string potionEffect => _potionEffect;

        [LabelText("포션 타입"), BoxGroup("기본 정보")]
        [PropertyOrder(6)]
        [ShowInInspector]
        private PotionType _potionType = PotionType.Status;
        public PotionType potionType => _potionType;

        [LabelText("포션 폭발 범위"), BoxGroup("기본 정보")]
        [PropertyOrder(7)]
        [ShowInInspector]
        private PotionExplosionRange _potionExplosionRange = PotionExplosionRange.Small;
        public PotionExplosionRange potionExplosionRange => _potionExplosionRange;

        [LabelText("포션 조합 타입"), BoxGroup("기본 정보")]
        [PropertyOrder(8)]
        [ShowInInspector]
        private PotionCombinationType _potionCombinationType = PotionCombinationType.Material;
        public PotionCombinationType potionCombinationType => _potionCombinationType;

        [BoxGroup("기본 정보")]
        [PropertyOrder(9)]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [ShowInInspector]
        private int _maxHoldCount = 1;
        public int maxHoldCount => _maxHoldCount;

        [HideIf("potionCombinationType", PotionCombinationType.Potion)]
        [SerializeField, LabelText("포션 레시피 재료 목록"), BoxGroup("레시피 정보")]
        [PropertyOrder(10)]
        private List<MaterialData> _recipeMaterials;
        public List<MaterialData> recipeMaterials => _recipeMaterials;

#if UNITY_EDITOR
        public void SetPotionID(int id)
        {
            _potionID = id;
        }
#endif
    }
}