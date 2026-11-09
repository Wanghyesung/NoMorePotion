using Enzeun.SO;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 규칙이 필요로 하는 게임 데이터(재료 한도, 포션/키워드 조회, 레시피)를 알려 주는 창구입니다.
    /// 규칙 클래스가 PotionDataManager(씬 오브젝트)를 직접 알지 않게 해서, 씬 없이도(가짜 데이터로) 시험할 수 있게 합니다.
    /// </summary>
    public interface IInventoryData
    {
        /// <summary>
        /// 알려진 재료인지 확인합니다.
        /// </summary>
        bool IsKnownMaterial(int materialId);

        /// <summary>
        /// 재료 한 종류의 보관 한도입니다. 공용 인벤에서는 종류별 총량, 개인 인벤에서는 한 칸에 쌓을 수 있는 개수입니다. (0 이면 보관 불가)
        /// </summary>
        int GetMaterialLimit(int materialId);

        bool TryGetPotion(int potionId, out PotionData data);

        bool TryGetKeyword(int keywordId, out TrapPotionKeyword keyword);

        /// <summary>
        /// 재료 두 개의 조합 결과입니다. 레시피가 없으면 null 입니다. (재료 순서와 무관)
        /// </summary>
        PotionData FindRecipe(int firstMaterialId, int secondMaterialId);

        /// <summary>
        /// 같은 포션 두 개를 합친 결과입니다. 합칠 수 없으면 null 입니다.
        /// </summary>
        PotionData FindUpgrade(int firstPotionId, int secondPotionId);
    }

    /// <summary>
    /// 씬의 PotionDataManager 와 InventoryRules 를 그대로 쓰는 기본 구현입니다.
    /// </summary>
    public sealed class DefaultInventoryData : IInventoryData
    {
        public static readonly DefaultInventoryData Instance = new DefaultInventoryData();

        private DefaultInventoryData() { }

        public bool IsKnownMaterial(int materialId)
        {
            return PotionDataManager.Instance != null
                && PotionDataManager.Instance.TryGetMaterialData(materialId, out MaterialData _);
        }

        public int GetMaterialLimit(int materialId)
        {
            return InventoryRules.GetMaterialLimit(materialId);
        }

        public bool TryGetPotion(int potionId, out PotionData data)
        {
            data = null;
            return PotionDataManager.Instance != null && PotionDataManager.Instance.TryGetPotionData(potionId, out data);
        }

        public bool TryGetKeyword(int keywordId, out TrapPotionKeyword keyword)
        {
            keyword = null;
            return PotionDataManager.Instance != null && PotionDataManager.Instance.TryGetTrapKeyword(keywordId, out keyword);
        }

        public PotionData FindRecipe(int firstMaterialId, int secondMaterialId)
        {
            return PotionDataManager.Instance != null
                ? PotionDataManager.Instance.GetPotionDataFromRecipe(firstMaterialId, secondMaterialId)
                : null;
        }

        public PotionData FindUpgrade(int firstPotionId, int secondPotionId)
        {
            return PotionDataManager.Instance != null
                ? PotionDataManager.Instance.GetUpgradedPotion(firstPotionId, secondPotionId)
                : null;
        }
    }
}
