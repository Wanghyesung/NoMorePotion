using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 포션 제작 결과를 판정해서 PotionInstance 를 만드는 정적 도우미입니다.
    /// 제작하는 순간 실패 확률로 성공/실패를 판정하고, 실패하면 함정 키워드를 균등 확률로 하나 붙입니다.
    ///
    /// 실패 확률은 호출하는 쪽이 넘기므로, 확률을 정하는 규칙이 바뀌어도 이 클래스는 바뀌지 않습니다.
    /// 기본 규칙은 플레이어에게 걸린 상태 효과의 "제조 실패 확률"입니다. (똑똑이 0%, 멍청함 50%)
    /// 어떤 상태 효과가 걸릴지는 생존 상태(정신력)가 정하고, 이 클래스는 걸려 있는 효과만 읽습니다.
    /// </summary>
    public static class PotionCrafter
    {
        /// <summary>
        /// 재료 두 개로 제작합니다. 레시피가 없으면 null 입니다. 실패 확률은 내 플레이어의 상태 효과에서 가져옵니다.
        /// </summary>
        public static PotionInstance CraftFromMaterials(int firstMaterialID, int secondMaterialID)
        {
            if (PotionDataManager.Instance == null)
            {
                Debug.LogWarning("[PotionCrafter]: PotionDataManager 가 없어 제작할 수 없습니다.");
                return null;
            }

            PotionData result = PotionDataManager.Instance.GetPotionDataFromRecipe(firstMaterialID, secondMaterialID);
            if (result == null)
            {
                return null;
            }

            return Craft(result, GetPlayerCraftFailChance());
        }

        /// <summary>
        /// 포션 두 개로 제작합니다. 조합할 수 없으면 null 입니다.
        /// </summary>
        /// <returns></returns>
        public static PotionInstance CraftFromPotion(int firstMaterialID, int secondMaterialID)
        {
            if (PotionDataManager.Instance == null)
            {
                Debug.LogWarning("[PotionCrafter]: PotionDataManager 가 없어 제작할 수 없습니다.");
                return null;
            }

            PotionData result = PotionDataManager.Instance.GetUpgradedPotion(firstMaterialID, secondMaterialID);
            if (result == null)
            {
                return null;
            }

            return Craft(result, GetPlayerCraftFailChance());
        }

        /// <summary>
        /// 제작한 포션 데이터로 인스턴스를 만듭니다. failChancePercent(0~100)의 확률로 실패해서 함정 포션이 됩니다.
        /// </summary>
        private static PotionInstance Craft(PotionData result, int failChancePercent)
        {
            if (result == null)
            {
                Debug.LogWarning("[PotionCrafter]: 제작 결과 포션 데이터가 null 입니다.");
                return null;
            }

            int chance = Mathf.Clamp(failChancePercent, 0, 100);

            // Random.Range(0, 100) 은 0~99 이므로 확률 0 이면 항상 성공, 100 이면 항상 실패합니다.
            if (Random.Range(0, 100) < chance)
            {
                TrapPotionKeyword keyword = PickTrapKeyword();

                if (keyword != null)
                {
                    return new PotionInstance(result, keyword);
                }

                Debug.LogWarning("[PotionCrafter]: 함정 키워드가 없어 정상 포션으로 제작합니다.");
            }

            return new PotionInstance(result);
        }

        /// <summary>
        /// 내 플레이어에게 걸린 상태 효과 기준 제조 실패 확률(%)입니다. 플레이어 컨테이너가 없으면 0 입니다.
        /// </summary>
        public static int GetPlayerCraftFailChance()
        {
            SurviveStatusManager manager = SurviveStatusManager.Instance;
            if (manager == null || manager.PlayerBuffContainer == null)
            {
                return 0;
            }

            return manager.PlayerBuffContainer.GetCraftFailChance();
        }

        /// <summary>
        /// 함정 키워드(5만번대) 중 하나를 균등 확률로 뽑습니다. 없으면 null 입니다.
        /// </summary>
        private static TrapPotionKeyword PickTrapKeyword()
        {
            if (PotionDataManager.Instance == null)
            {
                return null;
            }

            var pool = PotionDataManager.Instance.TrapKeywords;
            if (pool.Count == 0)
            {
                return null;
            }

            return pool[Random.Range(0, pool.Count)];
        }
    }
}
