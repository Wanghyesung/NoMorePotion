using UnityEngine;
using Sirenix.OdinInspector;


namespace Enzeun.SO
{

    [CreateAssetMenu(fileName = "MaterialData", menuName = "Scriptable Objects/MaterialData")]
    public class MaterialData : ScriptableObject
    {
        [PreviewField(100, ObjectFieldAlignment.Left)]
        [ShowInInspector] public Sprite materialIcon { get; private set; } = null;
        [LabelText("재료 ID"), BoxGroup("기본 정보")]
        [ShowInInspector] public int materialID { get; private set; } = 20001;
        [LabelText("재료 이름"), BoxGroup("기본 정보")]
        [ShowInInspector] public string materialName { get; private set; } = "이름 입니다";
        [LabelText("재료 설명"), BoxGroup("기본 정보")]
        [TextArea]
        [ShowInInspector] public string materialDesc { get; private set; } = "설명 입니다";

        [BoxGroup("기본 정보")]
        [MinValue(-1), LabelText("최대 보유 개수"), MaxValue(99)]
        [ShowInInspector] public int maxHoldCount { get; private set; } = 99;

    }
}