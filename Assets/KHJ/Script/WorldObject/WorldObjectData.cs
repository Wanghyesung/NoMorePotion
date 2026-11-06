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


    /// <summary>
    /// 월드 오브젝트의 공유 설정 데이터입니다. 규칙은 WorldObjectRules 참고.
    /// </summary>
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
        /// 규칙은 WorldObjectRules 참고. 0 이 되면 소진(드롭)됩니다.
        /// </summary>
        [LabelText("횟수 (차감되어 0 이 되면 소진)"), BoxGroup("기본 정보")]
        [SerializeField, MinValue(1)]
        [PropertyOrder(7)]
        private int _maxCount = 1;
        public int maxCount => _maxCount;

        [LabelText("드롭 재료와 개수"), BoxGroup("드롭 정보")]
        [SerializeField]
        [PropertyOrder(8)]
        private List<MaterialDrop> _drops = new List<MaterialDrop>();
        public IReadOnlyList<MaterialDrop> Drops => _drops;

        /// <summary>
        /// 드롭할 재료와 개수를 굴려서 results 에 담습니다. (results 는 먼저 비웁니다)
        /// 멀티플레이에서는 소진을 판정한 쪽(마스터)만 굴리고, 결과(재료 ID, 개수)만 공유해야 모든 클라이언트에서 같은 드롭이 됩니다.
        /// </summary>
        public void RollDrops(List<MaterialDropResult> results)
        {
            results.Clear();

            for (int i = 0; i < _drops.Count; i++)
            {
                MaterialDrop drop = _drops[i];

                if (drop == null || drop.Material == null)
                {
                    continue;
                }

                results.Add(new MaterialDropResult { MaterialId = drop.Material.materialID, Count = drop.Roll() });
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용: 생성 도구가 ID 를 지정한다.
        /// </summary>
        public void SetID(int id)
        {
            _worldObjectID = id;
        }
#endif

    }
}