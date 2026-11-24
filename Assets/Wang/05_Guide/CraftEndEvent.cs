using Enzeun.Runtime;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class CraftEndEvent : GuideEndEvent
{
    [SerializeField] private InventoryController inventory;
    [Header("인벤토리 안내")]
    [SerializeField] private GuideUIHighlight highlight;
    [SerializeField] private CombinationController combination;
    [SerializeField] private GameObject materialHighlight;
    [SerializeField] private GameObject craftHighlight;
    private CancellationTokenSource closeCancellation;

    protected override void OnBegin()
    {
        InventoryNetworkSyncManager.Instance.OnPotionCrafted += OnPotionCrafted;
        combination.OnIngredientsChanged += RefreshFocus;
        RefreshFocus();
    }

    protected override void OnStop()
    {
        closeCancellation?.Cancel();
        closeCancellation?.Dispose();
        closeCancellation = null;
        if (InventoryNetworkSyncManager.Instance != null)
            InventoryNetworkSyncManager.Instance.OnPotionCrafted -= OnPotionCrafted;
        combination.OnIngredientsChanged -= RefreshFocus;
        highlight.Hide();
    }

    private void RefreshFocus()
    {
        if (closeCancellation != null)
            return;
        //먼저 집은 쪽 말고 다른 쪽 하이라이트
        int lineIndex = !combination.HasFirstIngredient ? 0 : !combination.HasSecondIngredient ? 1 : 2;
        if (lineIndex == 2)
            highlight.Show(craftHighlight);
        else
            highlight.ShowRightClick(materialHighlight);
        ShowGuideText(lineIndex);
    }

    private void OnPotionCrafted(PotionInstance potion)
    {
        if (closeCancellation != null)
            return;

        closeCancellation = new CancellationTokenSource();
        highlight.Hide();
        ShowGuideText(3);
        CloseInventory(closeCancellation.Token).Forget();
    }

    private async UniTask CloseInventory(CancellationToken token)
    {
        // 조합 결과를 잠시 보여준 뒤 닫는다. 시계 정지와 무관하게 1초를 센다.
        bool canceled = await UniTask.Delay(1000, ignoreTimeScale: true,
            cancellationToken: token).SuppressCancellationThrow();
        if (canceled)
            return;

        // InventoryActive는 토글이므로 이미 닫힌 창을 다시 열지 않는다.
        if (inventory.IsOpen)
            await inventory.InventoryActive(token);

        Complete();
    }
}
