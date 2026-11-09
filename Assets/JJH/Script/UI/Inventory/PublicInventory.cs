using System;
using System.Collections.Generic;
using Enzeun.Runtime;
using Enzeun.SO;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

using SF = UnityEngine.SerializeField;

public class PublicInventory : ItemInventory
{
    [Header("재료 파트")]
    [SF] private RectTransform materialContent;
    [Tooltip("화면에 보여 줄 재료, 순서")]
    [SF] private List<MaterialData> materialOrder = new();

    [Header("포션 파트")]
    [SF] private RectTransform potionContent;
    [SF] private int minPotionSlots = 0;
    // ex. "37 / 100"
    [SF] private TextMeshProUGUI potionCountText; 

    /// <summary>
    /// 포션 칸을 눌렀을 때, 그 포션의 인스턴스 ID 반환
    /// </summary>
    public event Action<int> OnPotionClicked;

    private MaterialInventory materials;
    private PotionInventory potions;

    private readonly Dictionary<int, ItemInstance> materialItems = new();
    private readonly List<InventoryIcon> potionIcons = new();
    private readonly List<PotionInstance> sorted = new();
    private bool potionsUpdate;

    /// <summary>
    /// 정렬 기준 반환 함수
    /// <para>정렬 기준 : 종류 → 인스턴스 ID</para>
    /// </summary>
    private static readonly Comparison<PotionInstance> ByTypeThenId = (a, b) =>
    {
        int c = a.Data.potionID.CompareTo(b.Data.potionID);
        return c != 0 ? c : a.InstanceId.CompareTo(b.InstanceId);
    };

    private void OnEnable() => potionsUpdate = true;

    private void Start()
    {
        if (materials != null) return;

        // 네트워크 매니저가 있으면 그 사본을, 없으면 테스트용 로컬 인벤토리를 사용
        TeamInventoryState state = InvenManager != null ? InvenManager.State : null;

        if (state != null) Bind(state.Materials, state.Potions);
        else Bind(new MaterialInventory(), new PotionInventory());
    }

    public void Bind(MaterialInventory materialInventory, PotionInventory potionInventory)
    {
        Unbind();

        materials = materialInventory;
        potions = potionInventory;

        materials.OnChanged += OnMaterialChanged;
        potions.OnChanged += OnPotionChanged;

        BuildMaterialSection();
        potionsUpdate = true;
    }

    private void Unbind()
    {
        if (materials != null) materials.OnChanged -= OnMaterialChanged;
        if (potions != null) potions.OnChanged -= OnPotionChanged;
    }

    private void OnDestroy() => Unbind();

    #region 재료

    private void BuildMaterialSection()
    {
        for (int i = materialContent.childCount - 1; i >= 0; i--)
            Destroy(materialContent.GetChild(i).gameObject);

        materialItems.Clear();

        foreach (MaterialData data in materialOrder)
        {
            if (data == null) continue;

            int id = data.materialID;
            Sprite icon = data.materialIcon;

            var item = new ItemInstance(id, icon, InventorySlotKind.Material, materials.GetCount(id));
            materialItems[id] = item;

            InventoryIcon view = Instantiate(inven.iconPrefab, materialContent);
            view.SetParentInventory(this);
            view.Init(item);
        }
    }

    // 바뀐 재료 수정
    private void OnMaterialChanged(int materialId)
    {
        if (materialItems.TryGetValue(materialId, out ItemInstance item))
            item.ItemCount = materials.GetCount(materialId);
    }

    #endregion

    #region

    private void OnPotionChanged() => potionsUpdate = true;

    private void LateUpdate()
    {
        // 이번 업데이트에 포션이 업데이트 되었을 경우
        if (!potionsUpdate || potions == null) return;
        potionsUpdate = false;
        RefreshPotions();
    }

    private void RefreshPotions()
    {
        sorted.Clear();
        sorted.AddRange(potions.Items);
        sorted.Sort(ByTypeThenId);

        int visible = Mathf.Max(sorted.Count, minPotionSlots);

        while (potionIcons.Count < visible)
        {
            InventoryIcon view = Instantiate(inven.iconPrefab, potionContent);
            view.SetParentInventory(this);
            view.OnClicked += HandlePotionClicked;
            potionIcons.Add(view);
        }

        for (int i = 0; i < potionIcons.Count; i++)
        {
            InventoryIcon view = potionIcons[i];
            bool active = i < visible;
            if (view.gameObject.activeSelf != active) view.gameObject.SetActive(active);

            PotionInstance p = i < sorted.Count ? sorted[i] : null;
            int id = p != null ? p.InstanceId : 0;

            // 같은 포션이 이미 연결돼 있으면 스킵
            if (view.BoundInstanceId != id)
            {
                view.Init(p == null ? null
                    : new ItemInstance(p.Data.potionID, p.Data.potionIcon, InventorySlotKind.Potion, 1, id));
            }
        }

        if (potionCountText != null)
            potionCountText.SetText("{0} / {1}", potions.Count, potions.Capacity);
    }

    private void HandlePotionClicked(InventoryIcon view)
    {
        if (view.BoundInstanceId != 0)
            OnPotionClicked?.Invoke(view.BoundInstanceId);
    }

    #endregion

}