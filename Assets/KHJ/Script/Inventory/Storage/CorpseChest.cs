using Sirenix.OdinInspector;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 시체 상자의 화면 오브젝트입니다. 데이터(CorpseBox)는 인벤토리 상태에 있고, 이 컴포넌트는 그 상자 번호(CorpseId)에 묶여서 E 키로 시체 상자 패널을 여는 일만 합니다.
    /// CorpseBoxSpawner 가 만들고 없앱니다. 열기와 닫기 동작은 InventorySideChest 를 따릅니다.
    ///
    /// 꺼내기 전용 : 시체 상자에는 넣을 수 없습니다. 칸을 클릭하면 아이템이 내 개인 인벤으로 옮겨지고(CorpsePanel), 개인 인벤이 가득 차면 못 꺼냅니다.
    /// 마지막 아이템을 꺼내면 상자가 사라지고 인벤토리가 강제로 닫힙니다. (CorpsePanel)
    /// </summary>
    public class CorpseChest : InventorySideChest
    {
        [ShowInInspector, ReadOnly, LabelText("시체 상자 번호")]
        public int CorpseId { get; private set; }

        /// <summary>
        /// 데이터(CorpseBox)에 묶습니다. 만든 직후 한 번 호출합니다.
        /// </summary>
        public void Bind(CorpseBox box)
        {
            CorpseId = box.Id;
            gameObject.name = $"CorpseChest_{box.Id}";
        }

        protected override bool UsesStoragePanel => false;

        protected override bool TryBeginSession()
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;

            // 그 사이에 다른 사람이 다 꺼내서 사라졌으면 열지 않습니다.
            if (manager == null || manager.State == null || !manager.State.Corpses.ContainsKey(CorpseId))
            {
                return false;
            }

            StorageSession.SetOpenCorpse(CorpseId);
            return true;
        }

        protected override void EndSession()
        {
            // 다른 상자를 연 뒤라면 그 상태를 지우지 않습니다.
            if (StorageSession.OpenCorpseId == CorpseId)
            {
                StorageSession.SetOpenCorpse(0);
            }
        }
    }
}
