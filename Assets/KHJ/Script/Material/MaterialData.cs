using UnityEngine;
using Sirenix.OdinInspector;


namespace Enzeun.SO
{

    [CreateAssetMenu(fileName = "MaterialData", menuName = "Scriptable Objects/MaterialData")]
    public class MaterialData : ScriptableObject
    {
        [PreviewField(100, ObjectFieldAlignment.Left)]
        [SerializeField] private Sprite materialIcon = null;
        [LabelText("재료 ID"), BoxGroup("기본 정보")]
        [SerializeField] private int materialID = 20001;
        [LabelText("재료 이름"), BoxGroup("기본 정보")]

        [SerializeField] private string materialName = "이름 입니다";
        [LabelText("재료 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [SerializeField] private string materialDesc = "설명 입니다";

        [BoxGroup("기본 정보")]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [SerializeField] private int maxHoldCount = 1;

        public void SetMaterialId(int id)
        {
            materialID = id;
        }

        public int GetMaterialId()
        {
            return materialID;
        }
    }

}