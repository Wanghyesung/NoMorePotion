using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Enzeun.SO
{

    [CreateAssetMenu(fileName = "PotionData", menuName = "Scriptable Objects/PotionData")]
    public class PotionData : ScriptableObject
    {
        [PreviewField(100, ObjectFieldAlignment.Left)]
        [SerializeField] private Sprite potionIcon = null;
        [LabelText("포션 ID"), BoxGroup("기본 정보")]
        [SerializeField] private int potionID = 10001;
        [LabelText("포션 이름"), BoxGroup("기본 정보")]

        [SerializeField] private string potionName = "이름 입니다";
        [LabelText("포션 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [SerializeField] private string potionDesc = "설명 입니다";

        [BoxGroup("기본 정보")]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [SerializeField] private int maxHoldCount = 1;

        [SerializeField, LabelText("포션 레시피 재료 목록"), BoxGroup("레시피 정보")]
        public List<MaterialData> RecipeMaterials;


        public void SetPotionId(int id)
        {
            potionID = id;
        }

        public int GetPotionId()
        {
            return potionID;
        }
    }
}