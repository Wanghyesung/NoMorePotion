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

        [LabelText("드롭 재료"), BoxGroup("드롭 정보")]
        [SerializeField]
        [PropertyOrder(7)]
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