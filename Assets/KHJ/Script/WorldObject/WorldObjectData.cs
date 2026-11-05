using UnityEngine;
using Sirenix.OdinInspector;
using System.Collections.Generic;

namespace Enzeun.SO
{
    /// <summary>
    /// 월드 오브젝트 타입을 정의하는 열거형입니다. Flower, Tree, Mushroom, Grass, Mineral, Bone 순서입니다.
    /// </summary>
    public enum WorldObjectType
    {
        None,
        Flower,
        Tree,
        Mushroom,
        Grass,
        Mineral,
        Bone,
    }

    /// <summary>
    /// 월드 오브젝트 드롭 타입을 정의하는 열거형입니다. Interacting 은 상호작용 시 드롭, Destroying 은 파괴 시 드롭입니다.
    /// </summary>
    public enum WorldObjectDropType
    {
        None,
        Interacting,
        Destroying,
    }


    [CreateAssetMenu(fileName = "WorldObjectData", menuName = "Scriptable Objects/WorldObjectData")]
    public class WorldObjectData : ScriptableObject, IItemData
    {
        public int ID => _worldObjectID;

        [PreviewField(100, ObjectFieldAlignment.Left)]
        [SerializeField]
        [PropertyOrder(1)]
        [LabelText("월드 오브젝트 아이콘")]
        private Sprite _worldObjectIcon = null;
        public Sprite worldObjectIcon => _worldObjectIcon;

        [LabelText("월드 오브젝트 ID"), BoxGroup("기본 정보")]
        [SerializeField]
        [PropertyOrder(2)]
        private int _worldObjectID = 30001;
        public int worldObjectID => _worldObjectID;

        [LabelText("월드 오브젝트 이름"), BoxGroup("기본 정보")]
        [SerializeField]
        [PropertyOrder(3)]
        private string _worldObjectName = "이름 입니다";
        public string worldObjectName => _worldObjectName;

        [LabelText("월드 오브젝트 설명"), BoxGroup("기본 정보")]
        [SerializeField]
        [PropertyOrder(4)]
        private string _worldObjectDesc = "설명 입니다";
        public string worldObjectDesc => _worldObjectDesc;

        [LabelText("월드 오브젝트 타입"), BoxGroup("기본 정보")]
        [SerializeField]
        [PropertyOrder(5)]
        private WorldObjectType _worldObjectType = WorldObjectType.Flower;
        public WorldObjectType worldObjectType => _worldObjectType;

        [LabelText("드롭 타입"), BoxGroup("기본 정보")]
        [SerializeField]
        [PropertyOrder(6)]
        private WorldObjectDropType _worldObjectDropType = WorldObjectDropType.Interacting;
        public WorldObjectDropType worldObjectDropType => _worldObjectDropType;

        /// <summary>
        /// 횟수 규칙 : 상호작용 타입은 상호작용 1회당 1씩 차감(포션 대미지는 받지 않음), 파괴 타입은 맞는 종류의 대미지가 들어올 때만 대미지 수치만큼 차감합니다.
        /// 0 이 되면 소진(드롭)됩니다. 예) 나무(소) 1 / 나무(중) 2 / 나무(대) 3 -> 식물 대미지 소 1, 중 2, 대 3 으로 각각 한 번에 소진
        /// </summary>
        [LabelText("횟수 (차감되어 0 이 되면 소진)"), BoxGroup("기본 정보")]
        [SerializeField, MinValue(1)]
        [PropertyOrder(7)]
        private int _maxCount = 1;
        public int maxCount => _maxCount;

        [LabelText("드롭 재료"), BoxGroup("드롭 정보")]
        [SerializeField]
        [PropertyOrder(8)]
        private List<MaterialData> _dropMaterials = new List<MaterialData>();
        public List<MaterialData> DropMaterials => _dropMaterials;

#if UNITY_EDITOR
        public void SetID(int id)
        {
            _worldObjectID = id;
        }
#endif

    }
}