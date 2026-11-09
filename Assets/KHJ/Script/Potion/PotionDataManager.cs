using UnityEngine;
using System.Collections.Generic;
using Enzeun.SO;
using System;
using Sirenix.OdinInspector;

namespace Enzeun.Runtime
{

    public class PotionDataManager : MonoBehaviour
    {

        // ==================== 싱글턴 & 스태틱 설정 ============================
        // ==================== 언제든지 포션 데이터에 접근할 수 있도록 ============================

        public static PotionDataManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                // 중복 매니저는 데이터를 다시 불러오지 않고 제거합니다.
                Destroy(gameObject);
                return;
            }

            FindAllPotionRecipes();
            LoadTrapKeywords();
            LoadMaterials();
        }

        /// <summary>
        /// 저장소에 있는 모든 함정 키워드를 불러옵니다. (제작 실패 시 뽑을 후보)
        /// </summary>
        private void LoadTrapKeywords()
        {
            trapKeywords.Clear();
            trapKeywordByID.Clear();
            trapKeywords.AddRange(Resources.LoadAll<TrapPotionKeyword>(TrapPotionKeyword.FolderPath));

            foreach (TrapPotionKeyword keyword in trapKeywords)
            {
                trapKeywordByID[keyword.KeywordID] = keyword;
            }
        }

        /// <summary>
        /// 저장소에 있는 모든 재료 데이터를 ID 로 찾을 수 있게 불러옵니다. (인벤토리의 보관 한도, 네트워크로 받은 ID 복원에 사용)
        /// </summary>
        private void LoadMaterials()
        {
            materialDataByID.Clear();

            foreach (MaterialData material in Resources.LoadAll<MaterialData>(materialFolderPath))
            {
                materialDataByID[material.materialID] = material;
            }
        }

        // ID 로 데이터를 찾는 조회입니다. 인벤토리 패킹/언패킹처럼 ID 만 오가는 곳에서 사용합니다.
        /// <summary>포션 ID 로 조회합니다. 없으면 false 입니다.</summary>
        public bool TryGetPotionData(int potionID, out PotionData data) => potionDataByID.TryGetValue(potionID, out data);
        /// <summary>재료 ID 로 조회합니다. 없으면 false 입니다.</summary>
        public bool TryGetMaterialData(int materialID, out MaterialData data) => materialDataByID.TryGetValue(materialID, out data);
        /// <summary>함정 키워드 ID 로 조회합니다. 없으면 false 입니다.</summary>
        public bool TryGetTrapKeyword(int keywordID, out TrapPotionKeyword keyword) => trapKeywordByID.TryGetValue(keywordID, out keyword);

        private readonly Dictionary<int, TrapPotionKeyword> trapKeywordByID = new Dictionary<int, TrapPotionKeyword>();
        private readonly Dictionary<int, MaterialData> materialDataByID = new Dictionary<int, MaterialData>();

        // 포션 폴더 경로를 상수로 정의
        public static readonly string materialFolderPath = "ScriptableObjects/Materials";
        public static readonly string potionFolderPath = "ScriptableObjects/Potions";


        #region 포션 데이터 접근 메서드

        // ==================== 포션 레시피 찾기 로직 ============================

        public struct PotionRecipeSet : IEquatable<PotionRecipeSet>
        {
            public int firstIngredientID;
            public int secondIngredientID;

            public PotionRecipeSet(int firstIngredientID, int secondIngredientID)
            {
                this.firstIngredientID = firstIngredientID;
                this.secondIngredientID = secondIngredientID;
            }

            public bool Equals(PotionRecipeSet other)
                => firstIngredientID == other.firstIngredientID
                && secondIngredientID == other.secondIngredientID;

            public override bool Equals(object obj) => obj is PotionRecipeSet other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(firstIngredientID, secondIngredientID);
        }

        [ShowInInspector, ReadOnly]
        private Dictionary<PotionRecipeSet, PotionData> potionDataDictionary = new Dictionary<PotionRecipeSet, PotionData>();

        // 포션 ID 로 포션 데이터를 찾기 위한 딕셔너리 (포션 + 포션 조합에서 사용)
        private Dictionary<int, PotionData> potionDataByID = new Dictionary<int, PotionData>();

        // 함정 키워드(5만번대) 목록입니다. 제작 실패 시 이 중에서 균등 확률로 뽑습니다. 포션 SO 와 별도 폴더에서 불러옵니다.
        private readonly List<TrapPotionKeyword> trapKeywords = new List<TrapPotionKeyword>();
        public IReadOnlyList<TrapPotionKeyword> TrapKeywords => trapKeywords;

        private bool isDataInitialized = false;

        /// <summary>
        /// 저장소에 있는 모든 포션의 레시피 등록
        /// 재료 순서와 무관하게 오름차순 키로 등록하며, 재료 조합(Material) 포션만 레시피가 됩니다.
        /// </summary>
        private void FindAllPotionRecipes()
        {
            PotionData[] potionDataList = Resources.LoadAll<PotionData>(potionFolderPath);

            foreach (var potionData in potionDataList)
            {
                // 포션 + 포션 조합(업그레이드)에서 ID 로 조회할 수 있도록 모든 포션을 등록
                potionDataByID[potionData.potionID] = potionData;

                // 재료 조합 포션만 레시피로 등록합니다. (포션 조합은 GetUpgradedPotion, 조합 타입이 None 인 함정 포션 등은 조합 대상 아님)
                if (potionData.potionCombinationType != PotionCombinationType.Material)
                {
                    continue;
                }

                List<MaterialData> recipeMaterials = potionData.recipeMaterials;

                if (recipeMaterials.Count == 2)
                {
                    int firstIngredientID = recipeMaterials[0].materialID;
                    int secondIngredientID = recipeMaterials[1].materialID;

                    AddPotionRecipe(firstIngredientID, secondIngredientID, potionData);
                }
                else
                {
                    Debug.LogWarning($"[PotionDataManager]: 포션 {potionData.potionName}의 레시피 재료가 2개가 아닙니다.");
                }
            }

            isDataInitialized = true;

            ValidateUpgradeChain();
        }

        /// <summary>
        /// 포션 + 포션 조합 데이터가 규칙을 지키는지 확인하고, 어긋나면 경고합니다. (시작할 때 한 번)
        /// 조합 타입이 Potion 인 포션은 바로 앞 ID 의 포션이 존재하고, 그 크기가 한 단계 작아야 합니다.
        /// 같은 계열인지까지는 알 수 없어서 크기와 ID 순서만 확인합니다.
        /// </summary>
        private void ValidateUpgradeChain()
        {
            foreach (PotionData potion in potionDataByID.Values)
            {
                if (potion.potionCombinationType != PotionCombinationType.Potion)
                {
                    continue;
                }

                if (!potionDataByID.TryGetValue(potion.potionID - 1, out PotionData lower)
                    || lower.potionSize + 1 != potion.potionSize)
                {
                    Debug.LogWarning($"[PotionDataManager]: 포션 조합 포션 {potion.potionName}({potion.potionID})의 바로 앞 ID 포션이 한 단계 작은 크기가 아닙니다. 데이터를 확인하세요.");
                }
            }
        }

        /// <summary>
        /// 포션의 레시피를 추가하는 메서드
        /// 재료 순서와 무관하게 작은 ID, 큰 ID 순(오름차순)의 키로 등록합니다.
        /// </summary>
        /// <param name="firstIngredientID">재료1</param>
        /// <param name="secondIngredientID">재료2</param>
        /// <param name="potionData">포션 데이터</param>
        private void AddPotionRecipe(int firstIngredientID, int secondIngredientID, PotionData potionData)
        {
            // 재료 ID를 오름차순으로 정렬            
            int first = Math.Min(firstIngredientID, secondIngredientID);
            int second = Math.Max(firstIngredientID, secondIngredientID);

            PotionRecipeSet recipe = new PotionRecipeSet(first, second);

            potionDataDictionary[recipe] = potionData;
        }

        /// <summary>
        /// 포션 레시피를 기반으로 포션 데이터를 가져오는 메서드
        /// </summary>
        /// <param name="firstIngredientID">재료1</param>
        /// <param name="secondIngredientID">재료2</param>
        [Button("레시피로 포션 데이터 가져오기", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        public PotionData GetPotionDataFromRecipe(int firstIngredientID, int secondIngredientID)
        {
            if (!isDataInitialized)
            {
                Debug.LogError("[PotionDataManager]: 포션 데이터가 초기화되지 않았습니다. FindAllPotionRecipes()를 먼저 호출하세요.");
                return null;
            }

            // 재료 ID를 오름차순으로 정렬
            int first = Math.Min(firstIngredientID, secondIngredientID);
            int second = Math.Max(firstIngredientID, secondIngredientID);

            PotionRecipeSet recipe = new PotionRecipeSet(first, second);

            if (potionDataDictionary.TryGetValue(recipe, out PotionData potionData))
            {
                return potionData;
            }
            else
            {
                Debug.LogWarning($"[PotionDataManager]: 레시피에 해당하는 포션 데이터가 없습니다. 재료 ID: {first}, {second}");
                return null;
            }
        }

        /// <summary>
        /// 포션 + 포션 조합 결과를 가져오는 메서드입니다.
        /// 기획 규칙 : 중 = 소 + 소, 대 = 중 + 중. 같은 포션 2개를 합치면 다음 ID(ID + 1)의 포션이 결과이며,
        /// 그 포션의 조합 타입이 Potion 이고 크기가 정확히 한 단계 커야 합니다. 대는 더 올릴 수 없습니다.
        /// (계열을 구분하는 정보가 따로 없어서 "같은 계열은 ID 가 소, 중, 대 순서로 붙어 있다"는 약속을 크기로 검증합니다)
        /// </summary>
        /// <param name="firstPotionID">첫 번째 포션 ID</param>
        /// <param name="secondPotionID">두 번째 포션 ID</param>
        /// <returns>업그레이드된 포션 데이터. 조합할 수 없으면 null</returns>
        public PotionData GetUpgradedPotion(int firstPotionID, int secondPotionID)
        {
            if (!isDataInitialized)
            {
                Debug.LogError("[PotionDataManager]: 포션 데이터가 초기화되지 않았습니다. FindAllPotionRecipes()를 먼저 호출하세요.");
                return null;
            }

            if (firstPotionID != secondPotionID)
            {
                Debug.LogWarning($"[PotionDataManager]: 서로 다른 포션은 합칠 수 없습니다. 포션 ID: {firstPotionID}, {secondPotionID}");
                return null;
            }

            if (!potionDataByID.TryGetValue(firstPotionID, out PotionData currentPotion))
            {
                Debug.LogWarning($"[PotionDataManager]: 포션 데이터를 찾지 못했습니다. 포션 ID: {firstPotionID}");
                return null;
            }

            // 대는 가장 큰 크기라 더 올릴 수 없습니다.
            if (currentPotion.potionSize < PotionSize.Large
                && potionDataByID.TryGetValue(firstPotionID + 1, out PotionData upgradedPotion)
                && upgradedPotion.potionCombinationType == PotionCombinationType.Potion
                && upgradedPotion.potionSize == currentPotion.potionSize + 1)
            {
                return upgradedPotion;
            }

            Debug.LogWarning($"[PotionDataManager]: 더 이상 업그레이드할 수 없는 포션입니다. 포션 ID: {firstPotionID}");
            return null;
        }

        #endregion



#if UNITY_EDITOR
        #region GC 테스트 메서드
        /// <summary>
        /// 레시피 조회 시 발생하는 GC 할당량을 측정하는 테스트용 메서드입니다. (Play 모드에서 실행)
        /// amount 만큼 반복 조회한 뒤, 총 할당량과 1회당 평균 할당량을 로그로 출력합니다.
        /// </summary>
        /// <param name="amount">반복 조회 횟수</param>
        [Button("레시피 조회 GC 측정", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void MeasureLookupGC(int amount = 10000)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PotionDataManager]: Play 모드에서만 측정할 수 있습니다.");
                return;
            }

            // Profiler 는 PlayerLoop 안의 샘플/GC 만 기록하므로, 버튼은 예약만 하고 다음 Update 에서 실행합니다.
            pendingMeasureAmount = amount;
        }

        private int pendingMeasureAmount = 0;

        private void Update()
        {
            if (pendingMeasureAmount <= 0)
            {
                return;
            }

            int amount = pendingMeasureAmount;
            pendingMeasureAmount = 0;
            RunLookupGCMeasure(amount);
        }

        private void RunLookupGCMeasure(int amount)
        {
            if (!isDataInitialized || potionDataDictionary.Count == 0)
            {
                Debug.LogWarning("[PotionDataManager]: 측정할 레시피가 없습니다. Play 모드에서 실행하세요.");
                return;
            }

            if (amount < 1)
            {
                amount = 1;
            }

            // 등록된 레시피 중 하나를 측정 대상으로 사용 (조회에 성공하는 경로를 측정)
            PotionRecipeSet sample = default;
            foreach (PotionRecipeSet key in potionDataDictionary.Keys)
            {
                sample = key;
                break;
            }

            // 워밍업: 첫 호출의 일회성 비용이 측정에 섞이지 않도록 한 번 먼저 호출
            GetPotionDataFromRecipe(sample.firstIngredientID, sample.secondIngredientID);

            int gcCountBefore = GC.CollectionCount(0);
            long memoryBefore = GC.GetTotalMemory(false);

            UnityEngine.Profiling.Profiler.BeginSample("PotionRecipe Lookup");
            for (int i = 0; i < amount; i++)
            {
                GetPotionDataFromRecipe(sample.firstIngredientID, sample.secondIngredientID);
            }
            UnityEngine.Profiling.Profiler.EndSample();

            long memoryAfter = GC.GetTotalMemory(false);
            int gcCountAfter = GC.CollectionCount(0);

            long allocatedBytes = memoryAfter - memoryBefore;
            double bytesPerLookup = (double)allocatedBytes / amount;

            Debug.Log($"[PotionDataManager]: 레시피 조회 {amount}회 / 총 할당 약 {allocatedBytes} bytes / 1회당 약 {bytesPerLookup:F2} bytes");

            if (gcCountAfter != gcCountBefore)
            {
                Debug.LogWarning($"[PotionDataManager]: 측정 중 GC가 {gcCountAfter - gcCountBefore}회 발생해 값이 부정확할 수 있습니다. amount를 줄여 다시 측정하세요.");
            }
        }
        #endregion
#endif

    }

}