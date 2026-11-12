using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Unity.VisualScripting;

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

    /// <summary>
    /// 포션 크기를 정의하는 열거형입니다. 소,중,대 로 나누어져 있습니다.
    /// </summary>
    public enum PotionSize
    {
        Small,
        Medium,
        Large,
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

        [LabelText("포션 크기"), BoxGroup("기본 정보")]
        [PropertyOrder(7)]
        [SerializeField]
        private PotionSize _potionSize = PotionSize.Small;
        public PotionSize potionSize => _potionSize;

        [LabelText("포션 액체 색상"), BoxGroup("기본 정보")]
        [PropertyOrder(8)]
        [SerializeField]
        private Color _potionColor = Color.white;
        public Color potionColor => _potionColor;

        [LabelText("포션 폭발 범위"), BoxGroup("기본 정보")]
        [PropertyOrder(9)]
        [SerializeField]
        private PotionExplosionRange _potionExplosionRange = PotionExplosionRange.Small;
        public PotionExplosionRange potionExplosionRange => _potionExplosionRange;

        [LabelText("포션 조합 타입"), BoxGroup("기본 정보")]
        [PropertyOrder(10)]
        [SerializeField]
        private PotionCombinationType _potionCombinationType = PotionCombinationType.Material;
        public PotionCombinationType potionCombinationType => _potionCombinationType;

        [BoxGroup("기본 정보")]
        [PropertyOrder(11)]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [SerializeField]
        private int _maxHoldCount = 1;
        public int maxHoldCount => _maxHoldCount;

        // ========================================================

        [LabelText("폭발 프리팹"), BoxGroup("폭발 오브젝트")]
        [PropertyOrder(12)]
        [SerializeField]
        private GameObject _explosionPrefab;
        public GameObject explosionPrefab => _explosionPrefab;


        [HideIf("potionCombinationType", PotionCombinationType.Potion)]
        [SerializeField, LabelText("포션 레시피 재료 목록"), BoxGroup("레시피 정보")]
        [PropertyOrder(13)]
        private List<MaterialData> _recipeMaterials;
        public List<MaterialData> recipeMaterials => _recipeMaterials;

        [LabelText("효과 목록"), BoxGroup("효과 정보")]
        [PropertyOrder(14)]
        [SerializeReference]
        private List<PotionEffect> _effects = new List<PotionEffect>();
        public IReadOnlyList<PotionEffect> effects => _effects;

        /// <summary>
        /// 초당 대미지처럼 반복되는 체력 증감 효과의 반복 간격과 총 지속시간을 알려 줍니다. (범위 장판이 몇 초마다, 몇 번 틱을 줄지 정하는 기준)
        /// 간격과 지속시간이 모두 있는 체력 증감 효과가 여러 개면 가장 짧은 간격, 가장 긴 지속시간을 씁니다.
        /// </summary>
        /// <returns>반복되는 체력 증감 효과가 있으면 true</returns>
        public bool TryGetHealthTickSchedule(out float intervalSeconds, out float durationSeconds)
        {
            intervalSeconds = 0f;
            durationSeconds = 0f;

            if (_effects == null)
            {
                return false;
            }

            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] is HealthChangePotionEffect health
                    && health.healthChange.intervalSeconds > 0f && health.durationSeconds > 0f)
                {
                    intervalSeconds = intervalSeconds <= 0f ? health.healthChange.intervalSeconds : Mathf.Min(intervalSeconds, health.healthChange.intervalSeconds);
                    durationSeconds = Mathf.Max(durationSeconds, health.durationSeconds);
                }
            }

            return intervalSeconds > 0f && durationSeconds > 0f;
        }



#if UNITY_EDITOR
        public void SetID(int id)
        {
            _potionID = id;
        }
#endif
    }
}