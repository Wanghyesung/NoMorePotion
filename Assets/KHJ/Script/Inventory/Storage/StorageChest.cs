namespace Enzeun.Runtime
{
    /// <summary>
    /// 마을의 창고 상자입니다. 내 플레이어가 트리거 범위 안에서 상호작용 키(E)를 누르면 인벤토리를 열고 창고 패널을 펼칩니다.
    /// 열기와 닫기 동작은 InventorySideChest 를 따릅니다.
    /// 아이템 이동은 InventoryTransferRelay, 창고 내용 표시는 StoragePanel 이 맡습니다.
    ///
    /// 접근 제한 : 상자가 마을에만 있으므로 "상자에 다가갔다"를 마을 안으로 칩니다. 서버 검증(TeamInventoryState.SetStorageAccessGate)은 쓰지 않습니다. (클라이언트를 신뢰하는 방침)
    /// </summary>
    public class StorageChest : InventorySideChest
    {
        protected override bool UsesStoragePanel => true;

        protected override bool TryBeginSession()
        {
            StorageSession.SetOpen(true);
            return true;
        }

        protected override void EndSession()
        {
            StorageSession.SetOpen(false);
        }
    }
}
