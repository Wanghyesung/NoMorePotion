namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리들이 공통으로 쓰는 규칙입니다.
    /// </summary>
    public static class InventoryRules
    {
        /// <summary>
        /// 재료 데이터에 "제한 없음"(음수)이나 조회 실패일 때 적용하는 한 종류당 보관 한도입니다.
        /// </summary>
        public const int DefaultMaterialLimit = 99;

        /// <summary>
        /// 재료 한 종류의 보관 한도를 반환합니다.
        /// 재료 SO 의 "최대 보유 개수"를 따르며, 개인 인벤토리에서는 한 칸에 쌓을 수 있는 최대 개수, 팀 인벤토리에서는 종류별 총량 한도로 사용합니다.
        /// 값이 음수(제한 없음)이거나 데이터를 찾지 못하면 99, 0 이면 보관할 수 없는 재료입니다.
        /// </summary>
        public static int GetMaterialLimit(int materialId)
        {
            if (PotionDataManager.Instance != null
                && PotionDataManager.Instance.TryGetMaterialData(materialId, out Enzeun.SO.MaterialData data))
            {
                return data.maxHoldCount < 0 ? DefaultMaterialLimit : data.maxHoldCount;
            }

            return DefaultMaterialLimit;
        }
    }
}
