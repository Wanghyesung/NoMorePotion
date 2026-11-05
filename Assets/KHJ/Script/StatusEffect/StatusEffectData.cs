using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 상태 효과 종류를 정의하는 열거형입니다. 버프, 디버프로 나누어져 있습니다.
    /// </summary>
    public enum StatusEffectType
    {
        None,
        Buff,
        Debuff,
    }

    /// <summary>
    /// 같은 상태 효과가 다시 걸렸을 때의 중첩 규칙을 정의하는 열거형입니다.
    /// Unique : 하나만 유지 / RefreshDuration : 지속시간 갱신 / StrongestWins : 가장 강한 수치만 적용(지속시간은 각자)
    /// </summary>
    public enum StackPolicy
    {
        None,
        Unique,
        RefreshDuration,
        StrongestWins,
    }

    [CreateAssetMenu(fileName = "StatusEffectData", menuName = "Scriptable Objects/StatusEffectData")]
    public class StatusEffectData : ScriptableObject, IItemData
    {
        // 상태 효과 SO 가 저장되는 Resources 하위 폴더 경로
        public static readonly string folderPath = "ScriptableObjects/StatusEffects";

        public int ID => _statusEffectID;

        [PreviewField(100, ObjectFieldAlignment.Left)]
        [LabelText("상태 효과 아이콘")]
        [PropertyOrder(1)]
        [SerializeField]
        private Sprite _statusEffectIcon = null;
        public Sprite statusEffectIcon => _statusEffectIcon;

        [LabelText("상태 효과 ID"), BoxGroup("기본 정보")]
        [PropertyOrder(2)]
        [SerializeField]
        private int _statusEffectID = 60001;
        public int statusEffectID => _statusEffectID;

        [LabelText("상태 효과 이름"), BoxGroup("기본 정보")]
        [PropertyOrder(3)]
        [SerializeField]
        private string _statusEffectName = "이름 입니다";
        public string statusEffectName => _statusEffectName;

        [LabelText("상태 효과 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [PropertyOrder(4)]
        [SerializeField]
        private string _statusEffectDesc = "설명 입니다";
        public string statusEffectDesc => _statusEffectDesc;

        [LabelText("버프 / 디버프"), BoxGroup("기본 정보")]
        [PropertyOrder(5)]
        [SerializeField]
        private StatusEffectType _statusEffectType = StatusEffectType.None;
        public StatusEffectType statusEffectType => _statusEffectType;

        [LabelText("지속시간 유무"), BoxGroup("기본 정보")]
        [PropertyOrder(6)]
        [SerializeField]
        private bool _hasDuration = false;
        public bool hasDuration => _hasDuration;

        [LabelText("중첩 정책"), BoxGroup("기본 정보")]
        [PropertyOrder(7)]
        [SerializeField]
        private StackPolicy _stackPolicy = StackPolicy.None;
        public StackPolicy stackPolicy => _stackPolicy;

        [LabelText("효과 모듈 목록"), BoxGroup("효과 정보")]
        [PropertyOrder(8)]
        [SerializeReference]
        private List<StatusEffectModule> _modules = new List<StatusEffectModule>();
        public IReadOnlyList<StatusEffectModule> modules => _modules;

#if UNITY_EDITOR
        public void SetStatusEffectType(StatusEffectType type)
        {
            _statusEffectType = type;
        }

        public void SetID(int id)
        {
            _statusEffectID = id;
        }
#endif
    }
}
