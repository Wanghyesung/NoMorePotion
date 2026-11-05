using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Enzeun.SO
{
    /// <summary>
    /// 포션 타입을 정의하는 열거형입니다. 스탯 관리, 채집, 전투 로 나누어져 있습니다.
    /// </summary>
    public enum PotionType
    {
        None,
        Status,
        Gathering,
        Battle,
    }

    /// <summary>
    /// 포션 폭발 범위를 정의하는 열거형입니다. 소,중,대 로 나누어져 있습니다.
    /// </summary>
    public enum PotionExplosionRange
    {
        None,
        Small,
        Medium,
        Large,
    }

    /// <summary>
    /// 포션 조합 타입을 정의하는 열거형입니다. Material 은 재료 조합, Potion 은 하위 포션2개 조합입니다.
    /// </summary>
    public enum PotionCombinationType
    {
        None,
        Material,
        Potion,
    }

    [CreateAssetMenu(fileName = "PotionData", menuName = "Scriptable Objects/PotionData")]
    public class PotionData : ScriptableObject, IItemData
    {
        public int ID => _potionID;

        [PreviewField(100, ObjectFieldAlignment.Left)]
        [LabelText("포션 아이콘")]
        [PropertyOrder(1)]
        [SerializeField]
        private Sprite _potionIcon = null;
        public Sprite potionIcon => _potionIcon;

        [LabelText("포션 ID"), BoxGroup("기본 정보")]
        [PropertyOrder(2)]
        [SerializeField]
        private int _potionID = 10001;
        public int potionID => _potionID;

        [LabelText("포션 이름"), BoxGroup("기본 정보")]
        [PropertyOrder(3)]
        [SerializeField]
        private string _potionName = "이름 입니다";
        public string potionName => _potionName;

        [LabelText("포션 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [PropertyOrder(4)]
        [SerializeField]
        private string _potionDesc = "설명 입니다";
        public string potionDesc => _potionDesc;

        [LabelText("포션 효과"), BoxGroup("기본 정보")]
        [TextArea]
        [PropertyOrder(5)]
        [SerializeField]
        private string _potionEffect = "효과 입니다";
        public string potionEffect => _potionEffect;

        [LabelText("포션 타입"), BoxGroup("기본 정보")]
        [PropertyOrder(6)]
        [SerializeField]
        private PotionType _potionType = PotionType.Status;
        public PotionType potionType => _potionType;

        [LabelText("포션 폭발 범위"), BoxGroup("기본 정보")]
        [PropertyOrder(7)]
        [SerializeField]
        private PotionExplosionRange _potionExplosionRange = PotionExplosionRange.Small;
        public PotionExplosionRange potionExplosionRange => _potionExplosionRange;

        [LabelText("포션 조합 타입"), BoxGroup("기본 정보")]
        [PropertyOrder(8)]
        [SerializeField]
        private PotionCombinationType _potionCombinationType = PotionCombinationType.Material;
        public PotionCombinationType potionCombinationType => _potionCombinationType;

        [BoxGroup("기본 정보")]
        [PropertyOrder(9)]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [SerializeField]
        private int _maxHoldCount = 1;
        public int maxHoldCount => _maxHoldCount;

        [HideIf("potionCombinationType", PotionCombinationType.Potion)]
        [SerializeField, LabelText("포션 레시피 재료 목록"), BoxGroup("레시피 정보")]
        [PropertyOrder(10)]
        private List<MaterialData> _recipeMaterials;
        public List<MaterialData> recipeMaterials => _recipeMaterials;

        [LabelText("효과 목록"), BoxGroup("효과 정보")]
        [PropertyOrder(11)]
        [SerializeReference]
        private List<PotionEffect> _effects = new List<PotionEffect>();
        public IReadOnlyList<PotionEffect> effects => _effects;

#if UNITY_EDITOR
        public void SetID(int id)
        {
            _potionID = id;
        }
#endif
    }
}