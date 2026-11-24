using Enzeun.Runtime;
using Enzeun.SO;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using SF = UnityEngine.SerializeField;

public class StatusTooltip : MonoBehaviour
{
    public static StatusTooltip Instance { get; private set; }

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

    private List<EffectTextGroup> statusEffectList;
    private static BuffContainer PlayerContainer =>
        SurviveStatusManager.Instance != null ? SurviveStatusManager.Instance.PlayerBuffContainer : null;
    private void Awake()
    {
        Instance = this;
        statusEffectList = new();
    }

    #region Json 데이터 대체
    //Json 호출로 뺄 수 있으면 뺄 것

    private readonly string craftFailText = "포션 제조 실패율";

    private readonly Dictionary<StatusEffectType, string> statusEffectText = new()
    {
        [StatusEffectType.None] = "",
        [StatusEffectType.Buff] = "버프",
        [StatusEffectType.Debuff] = "디버프",
    };

    private readonly Dictionary<StatType, string> statTypeText = new()
    {
        [StatType.None] = "",
        [StatType.MoveSpeed] = "이동속도",
        [StatType.AttackPower] = "주는 피해",
        [StatType.Defense] = "저항",
        [StatType.Vision] = "시야",
    };

    private readonly Dictionary<PeriodicActionType, string> periodicTypeText = new()
    {
        [PeriodicActionType.None] = "",
        [PeriodicActionType.StopPlayerMove] = "추위",
        [PeriodicActionType.ConsumeFirstItem] = "허기",
        [PeriodicActionType.DrinkHeldPotion] = "갈증",
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

    private readonly Vector3[] corners = new Vector3[4]; // 0 좌하, 1 좌상, 2 우상, 3 우하
    private readonly string increaseColor = "#8FBF84";
    private readonly string decreaseColor = "#E0775A";
    #endregion

    /// <summary>
    /// 포션 정보창 호출
    /// </summary>
    /// <param name="item"></param>
    /// <param name="anchor"></param>
    public void Show(StatusEffectData item, RectTransform anchor)
    {
        string name = "";
        string info = "";
        string type = "";
        tooltip.alpha = 0;
        tooltip.gameObject.SetActive(true);
        
        for (int i = item.modules.Count; i < statusEffectList.Count; i++)
            statusEffectList[i].Release();

        for (int i = 0; i < item.modules.Count; i++)
        {
            (string effectName, string effectText) output =
                GetEffectText(item, item.modules[i]);

            // 생성된 객체가 있을 경우 재사용
            if (i < statusEffectList.Count)
                statusEffectList[i].Init(output.effectName, output.effectText);

            // 생성된 텍스트 리스트가 모자랄 경우 1회 생성
            else
            {
                EffectTextGroup dummy = Instantiate(effectTextPrefab, potionEffect);
                dummy.Init(output.effectName, output.effectText);

                statusEffectList.Add(dummy);
            }
        }

        if(item.modules.Count <= 0)
        {
            if(potionEffectDivideInfo.activeSelf)
                potionEffectDivideInfo.SetActive(false);
            if(potionEffect.gameObject.activeSelf)
                potionEffect.gameObject.SetActive(false);
        }
        else
        {
            if (!potionEffectDivideInfo.activeSelf)
                potionEffectDivideInfo.SetActive(true);
            if (!potionEffect.gameObject.activeSelf)
                potionEffect.gameObject.SetActive(true);
        }

        //이름
        name = item.statusEffectName;
        //설명
        info = item.statusEffectDesc;
        // 타입
        type = statusEffectText[item.statusEffectType];

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
        foreach (var item in statusEffectList)
            item.Release();
    }

    public (string effectName, string effectText) GetEffectText(StatusEffectData data, StatusEffectModule effect)
    {
        string nametext = "";
        string effectText = "";

        switch (effect)
        {
            case StatModifierModule statEffect:
                {
                    nametext = statTypeText[statEffect.stat];
                    int applyEffect = GetAppliedPercent(data, statEffect);
                    string hex = applyEffect >= 0 ? increaseColor : decreaseColor;

                    effectText = $"<color={hex}>{applyEffect}</color>%";
                    return (nametext, effectText);
                }
            case CraftFailChanceModule craftEffect:
                {
                    string hex = decreaseColor;

                    nametext = craftFailText;
                    effectText = $"<color={hex}>{craftEffect.FailPercent}</color>%";

                    return (nametext, effectText);
                }
            case HealthChangeModule healthEffect:
                {
                    string nametextRightSide
                        = healChangeText[healthEffect.changeType];

                    if (healthEffect.changeType == HealthChangeType.Damage)
                    {
                        DamageType dType = healthEffect.damageType;
                        string left = damageText[dType];
                        nametext =
                            $"<color={damageColorText[dType]}>{left}</color> {nametextRightSide}";
                    }
                    else
                        nametext = nametextRightSide;

                    effectText = $"{healthEffect.amount}";

                    return (nametext, effectText);
                }
            case PeriodicActionModule worldObjectEffect:
                {
                    string targets = periodicTypeText[worldObjectEffect.ActionType];

                    nametext = targets;
                    effectText = $"";
                    return (nametext, effectText);
                }
            default:
                Debug.Log("효과 없음");
                return ("효과 없음", "");
        }
    }

    /// <summary>
    /// 지금 걸려 있는 건들을 반영한 효과의 수치
    /// <para>걸려 있지 않으면 SO 값</para>
    /// </summary>
    private int GetAppliedPercent(StatusEffectData data, StatModifierModule module)
    {
        BuffContainer container = PlayerContainer;
        if (container == null) return module.percent;

        IReadOnlyList<ActiveStatusEffect> list = data.statusEffectType == StatusEffectType.Buff
            ? container.buffs : container.debuffs;
        if (list == null) return module.percent;

        float multiplier = 1f;
        bool found = false;

        if (data.stackPolicy == StackPolicy.Stackable)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].data != data) continue;
                multiplier *= ToMultiplier(EffectivePercent(list[i], module));
                found = true;
            }
        }
        else
        {
            // 가장 큰 한 건 (동률이면 먼저 걸린 건)
            int best = int.MinValue;
            int bestPercent = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].data != data) continue;

                int percent = EffectivePercent(list[i], module);
                if (Mathf.Abs(percent) > best)
                {
                    best = Mathf.Abs(percent);
                    bestPercent = percent;
                    found = true;
                }
            }
            multiplier = ToMultiplier(bestPercent);
        }

        return found ? Mathf.RoundToInt((multiplier - 1f) * 100f) : module.percent;
    }

    private static int EffectivePercent(ActiveStatusEffect effect, StatModifierModule module) =>
        effect.percentOverride != 0 ? effect.percentOverride : module.percent;

    private static float ToMultiplier(int percent) => 1f + Mathf.Max(percent, -100) / 100f;
}
