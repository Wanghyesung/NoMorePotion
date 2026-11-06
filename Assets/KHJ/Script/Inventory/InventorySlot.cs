namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 한 칸에 들어 있는 것의 종류입니다. 비어 있음, 재료, 포션 순서입니다.
    /// </summary>
    public enum InventorySlotKind
    {
        Empty,
        Material,
        Potion,
    }

    /// <summary>
    /// 칸 기반 인벤토리(개인, 시체 상자)의 한 칸입니다. 재료 묶음(ID + 개수) 또는 포션 인스턴스 한 개를 담습니다.
    /// 아무것도 지정하지 않은 기본값(default)은 빈 칸입니다.
    /// </summary>
    public struct InventorySlot
    {
        public InventorySlotKind Kind;

        // 재료일 때만 사용합니다.
        public int MaterialId;
        public int Count;

        // 포션일 때만 사용합니다.
        public PotionInstance Potion;

        public bool IsEmpty => Kind == InventorySlotKind.Empty;

        /// <summary>
        /// 재료 묶음이 들어 있는 칸을 만듭니다.
        /// </summary>
        public static InventorySlot OfMaterial(int materialId, int count)
        {
            return new InventorySlot { Kind = InventorySlotKind.Material, MaterialId = materialId, Count = count };
        }

        /// <summary>
        /// 포션 한 개가 들어 있는 칸을 만듭니다.
        /// </summary>
        public static InventorySlot OfPotion(PotionInstance potion)
        {
            return new InventorySlot { Kind = InventorySlotKind.Potion, Potion = potion };
        }
    }
}
