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

    private void Start() => BindWhenReady().Forget();

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
        }
        else
        {
            inven.publicInventScrollRect.content = materialContent;
            potionContent.gameObject.SetActive(false);
            materialContent.gameObject.SetActive(true);
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