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

        public struct PotionRecipeSet
        {
            public int firstIngredientID;
            public int secondIngredientID;

            public PotionRecipeSet(int firstIngredientID, int secondIngredientID)
            {
                this.firstIngredientID = firstIngredientID;
                this.secondIngredientID = secondIngredientID;
            }
        }

        [ShowInInspector, ReadOnly]
        private Dictionary<PotionRecipeSet, PotionData> potionDataDictionary = new Dictionary<PotionRecipeSet, PotionData>();

        public bool isDataInitialized { get; private set; } = false;

        /// <summary>
        /// 저장소에 있는 모든 포션의 레시피 등록
        /// </summary>
        public void FindAllPotionRecipes()
        {
            if (isDataInitialized)
            {
                Debug.LogWarning("포션 데이터가 이미 초기화되었습니다.");
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
                    Debug.LogWarning($"포션 {potionData.potionName}의 레시피 재료가 2개가 아닙니다.");
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
        public void AddPotionRecipe(int firstIngredientID, int secondIngredientID, PotionData potionData)
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
        public PotionData GetPotionData(int firstIngredientID, int secondIngredientID)
        {
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
                Debug.LogWarning($"레시피에 해당하는 포션 데이터가 없습니다. 재료 ID: {first}, {second}");
                return null;
            }
        }

        #endregion
    }

}