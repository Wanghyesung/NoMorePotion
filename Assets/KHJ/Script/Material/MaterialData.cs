using UnityEngine;
using Sirenix.OdinInspector;


namespace Enzeun.SO
{

    [CreateAssetMenu(fileName = "MaterialData", menuName = "Scriptable Objects/MaterialData")]
    public class MaterialData : ScriptableObject, IItemData
    {
        public int ID => _materialID;

        [PreviewField(100, ObjectFieldAlignment.Left)]
        [LabelText("재료 아이콘")]
        [PropertyOrder(1)]
        [SerializeField]
        private Sprite _materialIcon = null;
        public Sprite materialIcon => _materialIcon;

        [LabelText("재료 ID"), BoxGroup("기본 정보")]
        [PropertyOrder(2)]
        [SerializeField]
        private int _materialID = 20001;
        public int materialID => _materialID;

        [LabelText("재료 이름"), BoxGroup("기본 정보")]
        [PropertyOrder(3)]
        [SerializeField]
        private string _materialName = "이름 입니다";
        public string materialName => _materialName;

        [LabelText("재료 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [PropertyOrder(4)]
        [SerializeField]
        private string _materialDesc = "설명 입니다";
        public string materialDesc => _materialDesc;

        [BoxGroup("기본 정보")]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [PropertyOrder(5)]
        [SerializeField]
        private int _maxHoldCount = 99;
        public int maxHoldCount => _maxHoldCount;

#if UNITY_EDITOR
        public void SetID(int id)
        {
            _materialID = id;
        }
#endif
    }
}