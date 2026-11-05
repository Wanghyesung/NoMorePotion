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
                Destroy(gameObject);
            }

            FindAllPotionRecipes();
        }

        // 포션 폴더 경로를 상수로 정의
        public static readonly string materialFolderPath = "ScriptableObjects/Materials";
        public static readonly string potionFolderPath = "ScriptableObjects/Potions";


        #region 포션 데이터 접근 메서드

        // ==================== 포션 레시피 찾기 로직 ============================

        // 수정 후 코드
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

        private bool isDataInitialized = false;

        /// <summary>
        /// 저장소에 있는 모든 포션의 레시피 등록
        /// </summary>
        private void FindAllPotionRecipes()
        {
            if (isDataInitialized)
            {
                Debug.LogWarning("[PotionDataManager]: 포션 데이터가 이미 초기화되었습니다.");
                return;
            }

            PotionData[] potionDataList = Resources.LoadAll<PotionData>(potionFolderPath);

            foreach (var potionData in potionDataList)
            {
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
        }

        /// <summary>
        /// 포션의 레시피를 추가하는 메서드
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
        /// <returns></returns>
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