using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 함정 포션 키워드 데이터입니다. (50000번대)
    /// 포션 제작에 실패해서 함정 포션이 되면 원래 포션에 이 키워드 하나가 붙고, 키워드의 효과가 원래 포션의 효과에 더해집니다.
    /// 예) 달팽이 : 느림보 디버프 30초 (20%) / 폭발 : 범위 폭발 피해
    /// </summary>
    [CreateAssetMenu(fileName = "TrapPotionKeyword", menuName = "Scriptable Objects/TrapPotionKeyword")]
    public class TrapPotionKeyword : ScriptableObject, IItemData
    {
        // 함정 키워드 SO 가 저장되는 Resources 하위 폴더 경로
        public static readonly string FolderPath = "ScriptableObjects/TrapPotionKeywords";

        public int ID => _keywordID;

        [LabelText("키워드 ID"), BoxGroup("기본 정보")]
        [PropertyOrder(1)]
        [SerializeField]
        private int _keywordID = 50001;
        public int KeywordID => _keywordID;

        [LabelText("키워드 이름"), BoxGroup("기본 정보")]
        [PropertyOrder(2)]
        [SerializeField]
        private string _keywordName = "이름 입니다";
        public string KeywordName => _keywordName;

        [LabelText("효과 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [PropertyOrder(3)]
        [SerializeField]
        private string _effectDescription = "";
        public string EffectDescription => _effectDescription;

        [LabelText("효과 목록"), BoxGroup("효과 정보")]
        [PropertyOrder(4)]
        [SerializeReference]
        private List<PotionEffect> _effects = new List<PotionEffect>();
        public IReadOnlyList<PotionEffect> Effects => _effects;

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용: 생성 도구가 ID 를 지정한다.
        /// </summary>
        public void SetID(int id)
        {
            _keywordID = id;
        }
#endif
    }
}
