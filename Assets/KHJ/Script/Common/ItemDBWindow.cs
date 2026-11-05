#if UNITY_EDITOR

using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using System.IO;
using Enzeun.Runtime;
using Enzeun.SO;
using System;

namespace Enzeun.Editor
{

    public class ItemDatabaseWindow : OdinMenuEditorWindow
    {
        public static readonly string materialFolderPath = "Assets/Resources/" + PotionDataManager.materialFolderPath;
        public static readonly string potionFolderPath = "Assets/Resources/" + PotionDataManager.potionFolderPath;
        public static readonly string worldObjectFolderPath = "Assets/Resources/" + WorldObjectDataManager.worldObjectFolderPath;
        public static readonly string statusEffectFolderPath = "Assets/Resources/" + StatusEffectData.folderPath;

        [SerializeField, LabelText("재료 ID"), HideLabel]
        private int lastMaterialID = 20001;

        [SerializeField, LabelText("포션 ID"), HideLabel]
        private int lastPotionID = 10001;

        [SerializeField, LabelText("월드 오브젝트 ID"), HideLabel]
        private int lastWorldObjectID = 30001;

        [SerializeField, LabelText("버프 ID"), HideLabel]
        private int lastBuffID = 60001;

        [SerializeField, LabelText("디버프 ID"), HideLabel]
        private int lastDebuffID = 61001;

        CreateButton<MaterialData> createMaterialButton;
        CreateButton<PotionData> createPotionButton;
        CreateButton<WorldObjectData> createWorldObjectButton;
        CreateButton<StatusEffectData> createBuffButton;
        CreateButton<StatusEffectData> createDebuffButton;

        [MenuItem("Tools/아이템 데이터 관리자")]
        private static void OpenWindow()
        {
            var window = GetWindow<ItemDatabaseWindow>();
            window.titleContent = new GUIContent("아이템 데이터 관리자");
            window.Show();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            createMaterialButton?.Dispose();
            createPotionButton?.Dispose();
            createWorldObjectButton?.Dispose();
            createBuffButton?.Dispose();
            createDebuffButton?.Dispose();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();

            createMaterialButton = new CreateButton<MaterialData>(lastMaterialID);
            createMaterialButton.OnCreate += data => lastMaterialID = CreateAsset(data, materialFolderPath);
            tree.Add("Material SO 생성", createMaterialButton);

            createPotionButton = new CreateButton<PotionData>(lastPotionID);
            createPotionButton.OnCreate += data => lastPotionID = CreateAsset(data, potionFolderPath);
            tree.Add("Potion SO 생성", createPotionButton);

            createWorldObjectButton = new CreateButton<WorldObjectData>(lastWorldObjectID);
            createWorldObjectButton.OnCreate += data => lastWorldObjectID = CreateAsset(data, worldObjectFolderPath);
            tree.Add("World Object SO 생성", createWorldObjectButton);

            createBuffButton = new CreateButton<StatusEffectData>(lastBuffID);
            createBuffButton.newData.SetStatusEffectType(StatusEffectType.Buff);
            createBuffButton.OnCreate += data => lastBuffID = CreateAsset(data, statusEffectFolderPath);
            tree.Add("Buff SO 생성", createBuffButton);

            createDebuffButton = new CreateButton<StatusEffectData>(lastDebuffID);
            createDebuffButton.newData.SetStatusEffectType(StatusEffectType.Debuff);
            createDebuffButton.OnCreate += data => lastDebuffID = CreateAsset(data, statusEffectFolderPath);
            tree.Add("Debuff SO 생성", createDebuffButton);

            tree.AddAllAssetsAtPath("재료 목록", materialFolderPath, typeof(MaterialData), true);
            tree.AddAllAssetsAtPath("포션 목록", potionFolderPath, typeof(PotionData), true);
            tree.AddAllAssetsAtPath("월드 오브젝트 목록", worldObjectFolderPath, typeof(WorldObjectData), true);
            tree.AddAllAssetsAtPath("버프/디버프 목록", statusEffectFolderPath, typeof(StatusEffectData), true);

            return tree;
        }


        /// <summary>
        /// SO 를 생성하는 버튼 클래스입니다. IItemData 를 구현한 SO 타입이면 재사용할 수 있습니다.
        /// </summary>
        public class CreateButton<T> where T : ScriptableObject, IItemData
        {
            public CreateButton(int newID)
            {
                newData = ScriptableObject.CreateInstance<T>();
                newData.SetID(newID);
            }

            [InlineEditor(objectFieldMode: InlineEditorObjectFieldModes.Hidden)]
            public T newData;

            public Action<T> OnCreate;

            [Button("생성", ButtonSizes.Large)]
            [GUIColor(0.4f, 0.8f, 1f)]
            private void OnCreateBtnClicked()
            {
                OnCreate?.Invoke(newData);
            }

            /// <summary>
            /// 아직 에셋으로 저장되지 않은 임시 인스턴스만 정리합니다.
            /// </summary>
            public void Dispose()
            {
                if (newData != null && !EditorUtility.IsPersistent(newData))
                {
                    UnityEngine.Object.DestroyImmediate(newData);
                }
            }
        }


        /// <summary>
        /// SO 를 에셋 파일({ID}.asset)로 저장합니다.
        /// </summary>
        /// <returns>다음에 사용할 ID. 생성에 실패하면 현재 ID 를 그대로 반환합니다.</returns>
        private int CreateAsset<T>(T data, string folderPath) where T : ScriptableObject, IItemData
        {
            int createdID = data.ID;
            string fileName = $"{createdID}.asset";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string path = Path.Combine(folderPath, fileName);

            // 중복된 이름의 파일이 이미 존재하는지 체크
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("생성 실패", $"이미 동일한 이름의 에셋이 존재합니다:\n{fileName}", "확인");
                return createdID;
            }

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            return createdID + 1;
        }
    }
}
#endif
