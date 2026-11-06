using UnityEngine;
using System.Collections.Generic;
using Enzeun.SO;
using System;
using Sirenix.OdinInspector;


namespace Enzeun.Runtime
{
    public class WorldObjectDataManager : MonoBehaviour
    {
        public static WorldObjectDataManager Instance { get; private set; }

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

            FindAllWorldObjectData();
        }

        // 월드 오브젝트 폴더 경로를 상수로 정의
        public static readonly string worldObjectFolderPath = "ScriptableObjects/WorldObjects";

        #region 월드 오브젝트 데이터 접근 메서드

        [ShowInInspector, ReadOnly]
        private Dictionary<int, WorldObjectData> worldObjectDataDictionary = new Dictionary<int, WorldObjectData>();

        private bool isDataInitialized = false;

        /// <summary>
        /// 저장소에 있는 모든 월드 오브젝트 데이터를 등록
        /// </summary>
        private void FindAllWorldObjectData()
        {
            if (isDataInitialized)
            {
                Debug.LogWarning("[WorldObjectDataManager]: 이미 월드 오브젝트 데이터가 초기화되었습니다.");
                return;
            }

            WorldObjectData[] allWorldObjects = Resources.LoadAll<WorldObjectData>(worldObjectFolderPath);

            foreach (var worldObject in allWorldObjects)
            {
                if (!worldObjectDataDictionary.ContainsKey(worldObject.worldObjectID))
                {
                    worldObjectDataDictionary.Add(worldObject.worldObjectID, worldObject);
                }
                else
                {
                    Debug.LogWarning($"[WorldObjectDataManager]: 중복된 월드 오브젝트 ID {worldObject.worldObjectID} 발견. 무시합니다.");
                }
            }

            isDataInitialized = true;
        }

        /// <summary>
        /// 월드 오브젝트 ID로 월드 오브젝트 데이터를 가져옵니다.
        /// </summary>
        /// <param name="worldObjectID">월드 오브젝트 ID</param>
        /// <returns>월드 오브젝트 데이터</returns>
        public WorldObjectData GetWorldObjectData(int worldObjectID)
        {
            if (!isDataInitialized)
            {
                Debug.LogError("[WorldObjectDataManager]: 월드 오브젝트 데이터가 초기화되지 않았습니다. FindAllWorldObjectData()를 먼저 호출하세요.");
                return null;
            }

            if (worldObjectDataDictionary.TryGetValue(worldObjectID, out WorldObjectData worldObjectData))
            {
                return worldObjectData;
            }
            else
            {
                Debug.LogWarning($"[WorldObjectDataManager]: 월드 오브젝트 ID {worldObjectID}에 해당하는 데이터를 찾을 수 없습니다.");
                return null;
            }
        }

        #endregion
    }
}