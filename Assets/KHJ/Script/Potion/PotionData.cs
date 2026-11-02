using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Enzeun.SO
{
    [CreateAssetMenu(fileName = "PotionData", menuName = "Scriptable Objects/PotionData")]
    public class PotionData : ScriptableObject
    {
        [PreviewField(100, ObjectFieldAlignment.Left)]
        [ShowInInspector] public Sprite potionIcon { get; private set; } = null;

        [LabelText("포션 ID"), BoxGroup("기본 정보")]
        [ShowInInspector] public int potionID { get; private set; } = 10001;

        [LabelText("포션 이름"), BoxGroup("기본 정보")]
        [ShowInInspector] public string potionName { get; private set; } = "이름 입니다";

        [LabelText("포션 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [ShowInInspector] public string potionDesc { get; private set; } = "설명 입니다";

        [BoxGroup("기본 정보")]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [ShowInInspector] public int maxHoldCount { get; private set; } = 1;

        [SerializeField, LabelText("포션 레시피 재료 목록"), BoxGroup("레시피 정보")]
        private List<MaterialData> RecipeMaterials;

        [HideInInspector] public List<MaterialData> recipeMaterials => RecipeMaterials;

    }
}