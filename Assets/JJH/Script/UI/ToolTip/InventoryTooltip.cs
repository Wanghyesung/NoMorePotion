using Enzeun.Runtime;
using Enzeun.SO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class InventoryTooltip : MonoBehaviour
{
    public static InventoryTooltip Instance { get; private set; }

    [Header("툴팁 텍스트 참조")]
    [SF] private Canvas rootCanvas;
    [SF] private CanvasGroup tooltip;
    [SF] private RectTransform tooltipRect;
    [SF] private TextMeshProUGUI itemName;
    [SF] private TextMeshProUGUI itemType;
    [SF] private RectTransform potionEffect;
    [SF] private TextMeshProUGUI itemInfo;
    [SF] private GameObject potionEffectDivideInfo;
    [SF] private float gap = 5f;

    [Header("툴팁 프리팹")]
    [SF] private EffectTextGroup effectTextPrefab;

    private List<EffectTextGroup> potionEffectList;

    private void Awake() {
        Instance = this;
        potionEffectList = new();
    }

    #region Json 데이터 대체
    //Json 호출로 뺄 수 있으면 뺄 것
    private readonly Dictionary<PotionType, string> potionText = new()
    {
        [PotionType.None] = "",
        [PotionType.Battle] = "전투",
        [PotionType.Gathering] = "채집",
        [PotionType.Status] = "상태변화"
    };

    private readonly Dictionary<SurviveStatusType, string> gaugeText = new()
    {
        [SurviveStatusType.None] = "",
        [SurviveStatusType.BodyTemperature] = "체온",
        [SurviveStatusType.MentalStrength] = "정신력",
        [SurviveStatusType.Hydration] = "수분",
        [SurviveStatusType.Satiation] = "포만감"
    };

    private readonly Dictionary<HealthChangeType, string> healChangeText = new()
    {
        [HealthChangeType.None] = "",
        [HealthChangeType.Damage] = "피해",
        [HealthChangeType.Heal] = "회복"
    };

    private readonly Dictionary<DamageType, string> damageText = new()
    {
        [DamageType.None] = "고정",
        [DamageType.Normal] = "일반",
        [DamageType.Fire] = "화염",
        [DamageType.Ice] = "얼음",
        [DamageType.Electric] = "전기",
        [DamageType.Dark] = "어둠",
    };

    private readonly Dictionary<DamageType, string> damageColorText = new()
    {
        [DamageType.None] = "#B8B2A7", // 고정
        [DamageType.Normal] = "#B8B2A7", // 일반
        [DamageType.Fire] = "#E0654A", // 화염
        [DamageType.Ice] = "#9FD4EE", // 얼음
        [DamageType.Electric] = "#E8C547", // 전기
        [DamageType.Dark] = "#A77BD6", // 어둠
    };

    private readonly Dictionary<WorldObjectType, string> typeText = new()
    {
        [WorldObjectType.Tree] = "나무",
        [WorldObjectType.Flower] = "꽃",
        [WorldObjectType.Grass] = "풀",
        [WorldObjectType.Mushroom] = "버섯",
        [WorldObjectType.Mineral] = "광석",
        [WorldObjectType.Bone] = "뼈",
        [WorldObjectType.None] = "",

    };

    private static readonly WorldObjectType[] PlantTypes =
        { WorldObjectType.Tree, WorldObjectType.Flower, WorldObjectType.Grass, WorldObjectType.Mushroom };
    private static readonly WorldObjectType[] MineralTypes =
        { WorldObjectType.Mineral, WorldObjectType.Bone };

    private readonly List<string> targetParts = new();
    private readonly Vector3[] corners = new Vector3[4]; // 0 좌하, 1 좌상, 2 우상, 3 우하
    private readonly string increaseColor = "#8FBF84";
    private readonly string decreaseColor = "#E0775A";
    private readonly string destroyedObjectColor = "#D2AE66";
    #endregion

    /// <summary>
    /// 포션 정보창 호출
    /// </summary>
    /// <param name="item"></param>
    /// <param name="anchor"></param>
    public void Show(ItemInstance item, RectTransform anchor)
    {
        string name = "";
        string info = "";
        string type = "";
        tooltip.alpha = 0;
        tooltip.gameObject.SetActive(true);
        var dataManager = PotionDataManager.Instance;

        // 포션일 경우
        if (item.kind == InventorySlotKind.Potion &&
            dataManager.TryGetPotionData(item.itemId, out var potion))
        {
            for (int i = potion.effects.Count; i < potionEffectList.Count; i++)
                potionEffectList[i].Release();

            // 포션 효과 활성화
            potionEffectDivideInfo.SetActive(true);
            potionEffect.gameObject.SetActive(true);

            for(int i = 0; i < potion.effects.Count; i++)
            {
                (string effectName, string effectText) output = 
                    GetEffectText(potion.effects[i]);

                // 생성된 객체가 있을 경우 재사용
                if (i < potionEffectList.Count)
                    potionEffectList[i].Init(output.effectName, output.effectText);

                // 생성된 텍스트 리스트가 모자랄 경우 1회 생성
                else
                {
                    EffectTextGroup dummy = Instantiate(effectTextPrefab, potionEffect);
                    dummy.Init(output.effectName, output.effectText);

                    potionEffectList.Add(dummy);
                }
            }
            
            //이름
            name = potion.potionName;
            //설명
            info = potion.potionDesc;
            // 타입
            type = potionText[potion.potionType];
        }
        // 재료일 경우
        else if (dataManager.TryGetMaterialData(item.itemId, out var material))
        {
            // 포션 효과 비활성화
            potionEffectDivideInfo.SetActive(false);
            potionEffect.gameObject.SetActive(false);

            //이름
            name = material.materialName;
            //설명
            info = material.materialDesc;
            // 타입
            type = "재료";
        }

        itemName.SetText(name);
        itemInfo.SetText(info);
        itemType.SetText(type);
        

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

        anchor.GetWorldCorners(corners);
        float scale = rootCanvas.scaleFactor;
        Vector3 topRight = corners[2];

        tooltipRect.pivot = new Vector2(0f, 1f);
        tooltipRect.position = topRight + new Vector3(gap * scale, 0f, 0f);

        float width = tooltipRect.rect.width * scale;
        if (tooltipRect.position.x + width > Screen.width)
        {
            tooltipRect.pivot = new Vector2(1f, 1f);
            tooltipRect.position = corners[1] - new Vector3(gap * scale, 0f, 0f);
        }

        float height = tooltipRect.rect.height * scale;
        Vector3 p = tooltipRect.position;
        if (p.y - height < 0f) tooltipRect.position = new Vector3(p.x, height, p.z);

        tooltip.alpha = 1;
    }

    public void Hide()
    {
        tooltip.gameObject.SetActive(false);
        tooltip.alpha = 0;
        // 이펙트 리스트 정리
        foreach (var item in potionEffectList)
            item.Release();
        
    }

    public (string effectName, string effectText) GetEffectText(PotionEffect effect)
    {
        string nametext = "";
        string effectText = "";

        switch (effect)
        {
            case ApplyStatusPotionEffect applyEffect:
                { 
                nametext = applyEffect.statusEffect.statusEffectName;

                string durationText = applyEffect.durationSeconds <= 0 ?
                    "즉발" : $"{applyEffect.durationSeconds:0.#}초";

                string hex = "";

                if (applyEffect.percent >= 0)
                    hex = increaseColor;
                else
                    hex = decreaseColor;

                effectText = $"<color={hex}>{applyEffect.percent}</color>% / {durationText}";

                return (nametext, effectText);
                }
            case GaugeChangePotionEffect gaugeEffect:
                {
                    string hex = "";

                    if (gaugeEffect.percent >= 0)
                        hex = increaseColor;
                    else
                        hex = decreaseColor;

                    nametext = gaugeText[gaugeEffect.gauge];
                    effectText = $"<color={hex}>{gaugeEffect.percent}</color>%";

                    return (nametext, effectText);
                }
            case HealthChangePotionEffect healthEffect:
                {
                    string nametextRightSide
                        = healChangeText[healthEffect.healthChange.changeType];

                    if (healthEffect.healthChange.changeType == HealthChangeType.Damage)
                    {
                        DamageType dType = healthEffect.healthChange.damageType;
                        string left = damageText[dType];
                        nametext =
                            $"<color={damageColorText[dType]}>{left}</color> {nametextRightSide}";
                    }
                    else
                        nametext = nametextRightSide;


                    if (healthEffect.durationSeconds <= 0 ||
                        healthEffect.healthChange.intervalSeconds <= 0)
                        effectText = $"{healthEffect.healthChange.amount} / 즉발";

                    else
                    {
                        float perSecond =
                            healthEffect.healthChange.amount /
                            healthEffect.healthChange.intervalSeconds;
                        float duration = healthEffect.durationSeconds;
                        effectText =
                            $"초당 {perSecond:0.#} / {duration:0.#}초";
                    }

                    return (nametext, effectText);
                }
            case WorldObjectDamagePotionEffect worldObjectEffect:
                {
                    var targets = worldObjectEffect.targetTypes;
                    targetParts.Clear();

                    AppendGroup(targets, PlantTypes, "초목");
                    AppendGroup(targets, MineralTypes, "광물");

                    nametext = "파괴 가능";
                    effectText = $"<color={destroyedObjectColor}>{string.Join(", ", targetParts)}</color>";
                    return (nametext, effectText);
                }
            default:
                Debug.Log("효과가 없는 포션");
                return ("효과 없음", "");
        }
    }

    /// <summary>
    /// 채집/채광 텍스트 그룹화 함수
    /// </summary>
    /// <param name="targets">대상 그룹</param>
    /// <param name="group">비교할 그룹</param>
    /// <param name="groupName">반환할 이름</param>
    private void AppendGroup(IEnumerable<WorldObjectType> targets, 
        WorldObjectType[] group, string groupName)
    {
        if (group.All(targets.Contains))
        {
            targetParts.Add(groupName);
            return;
        }

        foreach (WorldObjectType type in group)
            if (targets.Contains(type)) targetParts.Add(typeText[type]);
    }
}
