using Cysharp.Threading.Tasks;
using DG.Tweening;
using Enzeun.Runtime;
using Enzeun.SO;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.TerrainTools;
using UnityEngine.UI;
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

    [Header("버튼 파트")]
    [SF] private Button potionTabBtn;
    [SF] private Button materialTabBtn;
    [SF] private RectTransform continueGroup;
    [SF] private Image buttonBackgroundAnimationImage;
    [SF] private AnimationCurve btnMoveEase;
    [SF] private AnimationCurve btnSizeEase;

    /// <summary>
    /// 포션 칸을 눌렀을 때, 그 포션의 인스턴스 ID 반환
    /// </summary>
    public event Action<int> OnPotionClicked;

    private MaterialInventory materials;
    private PotionInventory potions;

    private readonly Dictionary<int, ItemInstance> materialItems = new();
    private readonly Dictionary<int, InventoryIcon> materialViews = new();
    private readonly List<InventoryIcon> potionIcons = new();
    private readonly List<PotionInstance> sorted = new();
    private bool potionsUpdate;
    private bool btnType = true;
    private readonly Color imageTransf = new(1, 1, 1, 0);
    private readonly Color imageNormal = new(1, 1, 1, 1);

    private CancellationTokenSource btnAnimeToken;

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
        SubscribeCombineHighlight(); // 현준 수정
        BindWhenReady().Forget();
    }

    private async UniTaskVoid BindWhenReady()
    {
        if (materials != null) return;

        if (InvenManager == null || InvenManager.State == null)
            Debug.Log("[PublicInven] 매니저 대기");

        bool canceled = await UniTask.WaitUntil(
                () => InvenManager != null && InvenManager.State != null,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();

        if (canceled || materials != null) return;

        TeamInventoryState state = InvenManager.State;
        Bind(state.Materials, state.Potions);
        Debug.Log("[PublicInven] 매니저 연결 완료");
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

    private void OnDestroy()
    {
        Unbind();
        UnsubscribeCombineHighlight(); // 현준 수정
        btnAnimeToken?.Cancel();
        btnAnimeToken?.Dispose();
    }

    public void SlotChange()
    {
        btnAnimeToken?.Cancel();
        btnAnimeToken = new();

        if (btnType)
        {
            inven.publicInventScrollRect.content = potionContent;
            materialContent.gameObject.SetActive(false);
            potionContent.gameObject.SetActive(true);
            continueGroup.gameObject.SetActive(false);
        }
        else
        {
            inven.publicInventScrollRect.content = materialContent;
            potionContent.gameObject.SetActive(false);
            materialContent.gameObject.SetActive(true);
            continueGroup.gameObject.SetActive(true);
        }

        ButtonAnimation(btnType, btnAnimeToken.Token).Forget();

        btnType = !btnType;
    }

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
            int count = materials.GetCount(id);

            var item = new ItemInstance(id, icon, InventorySlotKind.Material, materials.GetCount(id));
            materialItems[id] = item;

            InventoryIcon view = Instantiate(inven.iconPrefab, materialContent);
            view.SetParentInventory(this);
            view.Init(item);
            view.gameObject.SetActive(count > 0);
            materialViews[id] = view;
            RefreshHighlightView(id, view); // 현준 수정 : 새로 만든 아이콘도 현재 강조 상태로
        }
    }

    // 바뀐 재료 수정
    private void OnMaterialChanged(int materialId)
    {
        if (!materialItems.TryGetValue(materialId, out ItemInstance item)) return;

        int count = materials.GetCount(materialId);
        item.ItemCount = count;

        if (materialViews.TryGetValue(materialId, out InventoryIcon view)
        && view.gameObject.activeSelf != (count > 0))
            view.gameObject.SetActive(count > 0);

        // 현준 수정 : 강조 중에 개수가 바뀌면 그 재료만 다시 판정 (같은 재료 짝은 개수 2개 기준이라 바뀔 수 있음)
        if (view != null && highlightMaterialId != 0)
            RefreshHighlightView(materialId, view);
    }

    #endregion

    #region CombineHighlight
    // ========================= 현준 수정 =============================
    // 조합 칸 왼쪽에 재료를 올리면, 그 재료와 조합할 수 있는 재료 아이콘에 체크 이미지를 띄웁니다.
    // 재료는 공용 인벤에만 들어가므로 개인 인벤 칸은 보지 않습니다. (규칙이 바뀌어 개인 칸에 재료가 들어가면 그쪽도 추가해야 함)

    // 창고 패널처럼 조합에 쓸 수 없는 목록은 StoragePanel 이 꺼 둡니다.
    private bool combineHighlightEnabled = true;
    private int highlightMaterialId;
    private CombinationController subscribedCombiner;

    private void SubscribeCombineHighlight()
    {
        if (potionCombined == null || subscribedCombiner != null) return;

        subscribedCombiner = potionCombined;
        subscribedCombiner.OnHighlightMaterialChanged += ApplyCombineHighlight;
    }

    private void UnsubscribeCombineHighlight()
    {
        if (subscribedCombiner == null) return;

        subscribedCombiner.OnHighlightMaterialChanged -= ApplyCombineHighlight;
        subscribedCombiner = null;
    }

    /// <summary>
    /// 조합 짝 강조를 켜거나 끕니다. 끄면 지금 떠 있는 체크도 모두 지웁니다. (창고 패널용)
    /// </summary>
    public void SetCombineHighlightEnabled(bool enable)
    {
        combineHighlightEnabled = enable;

        if (!enable)
            ApplyCombineHighlight(0);
    }

    // 기준 재료가 바뀔 때마다 재료 아이콘 전체를 다시 표시합니다. 0 이면 모두 끕니다.
    private void ApplyCombineHighlight(int materialId)
    {
        highlightMaterialId = combineHighlightEnabled ? materialId : 0;

        foreach (KeyValuePair<int, InventoryIcon> pair in materialViews)
            RefreshHighlightView(pair.Key, pair.Value);
    }

    // 아이콘 하나의 체크 표시를 다시 정합니다.
    // 왼쪽 칸에 올린 재료 자신은, 자기 자신과도 조합되면(2개 이상) 회색을 풀어 "같은 재료로도 조합된다"를 보여 주고, 아니면 회색으로 둡니다.
    // 강조를 끌 때(기준 0)는 알파를 건드리지 않습니다. (오른쪽 칸을 채울 때 BoundSlot 이 건 회색, Clear 의 RestoreAlpha 를 덮지 않기 위함)
    private void RefreshHighlightView(int materialId, InventoryIcon view)
    {
        bool partner = IsCombinePartner(materialId);
        view.ShowCheckImage(partner);

        if (highlightMaterialId != 0 && materialId == highlightMaterialId)
            view.SetAlpha(partner);
    }

    // 지금 기준 재료와 실제로 조합할 수 있는 재료인지 판정합니다. 같은 재료끼리는 2개 이상 있어야 합니다.
    private bool IsCombinePartner(int materialId)
    {
        if (highlightMaterialId == 0 || materials == null || PotionDataManager.Instance == null) return false;
        if (!PotionDataManager.Instance.IsRecipePartner(highlightMaterialId, materialId)) return false;

        return materialId != highlightMaterialId || materials.GetCount(materialId) >= 2;
    }
    // =================================================================
    #endregion

    #region 포션 갱신 및 전환

    private void OnPotionChanged()
    {
        potionsUpdate = true;
    }

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


    #region 애니메이팅 유니태스크
    private async UniTask ButtonAnimation(bool type, CancellationToken token)
    {
        RectTransform rect = buttonBackgroundAnimationImage.rectTransform;
        RectTransform target = (type ? potionTabBtn : materialTabBtn).image.rectTransform;

        potionTabBtn.image.color = imageTransf;
        materialTabBtn.image.color = imageTransf;
        rect.gameObject.SetActive(true);
        GetEdges(rect, out float left, out float right);
        GetEdges(target, out float toL, out float toR);

        bool moveRight = toL > left;
        const float lead = 0.05f;   // 앞쪽 모서리
        const float trail = 0.2f;  // 뒤쪽 모서리
        AnimationCurve frontEase = btnMoveEase;  // 앞쪽 모서리
        AnimationCurve backEase = btnSizeEase;   // 뒤쪽 모서리

        Sequence seq = DOTween.Sequence()
            .Join(DOTween.To(() => left, x => left = x, toL, moveRight ? trail : lead)
            .SetEase(moveRight ? backEase : frontEase))

            .Join(DOTween.To(() => right, x => right = x, toR, moveRight ? lead : trail)
            .SetEase(moveRight ? frontEase : backEase))

            .OnUpdate(() => ApplyEdges(rect, left, right))
            .OnComplete(() => ApplyEdges(rect, toL, toR))
            .SetUpdate(true)
            .SetLink(rect.gameObject);

        bool canceled =
            await seq.ToUniTask(cancellationToken: token).SuppressCancellationThrow();
        if (canceled) return;

        rect.gameObject.SetActive(false);

        (type ? potionTabBtn : materialTabBtn).image.color = imageNormal;
    }

    /// <summary>
    /// 피벗과 상관없이 좌우 모서리의 x 좌표를 구하는 함수
    /// </summary>
    /// <param name="rect">변환할 Rect</param>
    /// <param name="left">왼쪽값</param>
    /// <param name="right">오른쪽값</param>
    private static void GetEdges(RectTransform rect, out float left, out float right)
    {
        float width = rect.rect.width;
        left = rect.anchoredPosition.x - rect.pivot.x * width;
        right = left + width;
    }

    /// <summary>
    /// 모서리 두 개로 크기와 위치를 다시 계산하는 함수
    /// </summary>
    /// <param name="rect">계산할 rect</param>
    /// <param name="left">왼쪽값</param>
    /// <param name="right">오른쪽값</param>
    private static void ApplyEdges(RectTransform rect, float left, float right)
    {
        float width = right - left;
        rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        rect.anchoredPosition = new Vector2(left + rect.pivot.x * width, rect.anchoredPosition.y);
    }
    #endregion

}