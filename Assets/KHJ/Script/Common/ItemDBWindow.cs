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

        [SerializeField, LabelText("재료 ID"), HideLabel]
        private int lastMaterialID = 20001;

        [SerializeField, LabelText("포션 ID"), HideLabel]
        private int lastPotionID = 10001;

        [SerializeField, LabelText("월드 오브젝트 ID"), HideLabel]
        private int lastWorldObjectID = 30001;

        CreateMaterialButton createMaterialButton;
        CreatePotionButton createPotionButton;
        CreateWorldObjectButton createWorldObjectButton;

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

            if (createMaterialButton != null)
            {
                DestroyImmediate(createMaterialButton.newMaterial);
            }

            if (createPotionButton != null)
            {
                DestroyImmediate(createPotionButton.newPotion);
            }
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();

            createMaterialButton = new CreateMaterialButton(lastMaterialID);
            createMaterialButton.OnCreateMaterial += CreateMaterial;
            tree.Add("Material SO 생성", createMaterialButton);

            createPotionButton = new CreatePotionButton(lastPotionID);
            createPotionButton.OnCreatePotion += CreatePotion;
            tree.Add("Potion SO 생성", createPotionButton);

            createWorldObjectButton = new CreateWorldObjectButton(lastWorldObjectID);
            createWorldObjectButton.OnCreateWorldObject += CreateWorldObject;
            tree.Add("World Object SO 생성", createWorldObjectButton);

            tree.AddAllAssetsAtPath("재료 목록", materialFolderPath, typeof(MaterialData), true);
            tree.AddAllAssetsAtPath("포션 목록", potionFolderPath, typeof(PotionData), true);
            tree.AddAllAssetsAtPath("월드 오브젝트 목록", worldObjectFolderPath, typeof(WorldObjectData), true);

            return tree;
        }


        /// <summary>
        /// 재료 SO를 생성하는 버튼 클래스
        /// </summary>
        public class CreateMaterialButton
        {
            public CreateMaterialButton(int newMaterialID)
            {
                newMaterial = ScriptableObject.CreateInstance<MaterialData>();
                newMaterial.SetMaterialID(newMaterialID);
            }

            [InlineEditor(objectFieldMode: InlineEditorObjectFieldModes.Hidden)]
            public MaterialData newMaterial;

            [Button("재료 생성", ButtonSizes.Large)]
            [GUIColor(0.4f, 0.8f, 1f)]
            private void OnCreateMaterialBtnClicked()
            {
                OnCreateMaterial?.Invoke(newMaterial);
            }

            public Action<MaterialData> OnCreateMaterial;
        }

        /// <summary>
        /// 포션 SO를 생성하는 버튼 클래스
        /// </summary>
        public class CreatePotionButton
        {
            public CreatePotionButton(int newPotionID)
            {
                newPotion = ScriptableObject.CreateInstance<PotionData>();
                newPotion.SetPotionID(newPotionID);
            }

            [InlineEditor(objectFieldMode: InlineEditorObjectFieldModes.Hidden)]
            public PotionData newPotion;

            [Button("포션 생성", ButtonSizes.Large)]
            [GUIColor(0.4f, 0.8f, 1f)]
            private void OnCreatePotionBtnClicked()
            {
                OnCreatePotion?.Invoke(newPotion);
            }

            public Action<PotionData> OnCreatePotion;
        }


        /// <summary>
        /// 월드 오브젝트 SO를 생성하는 버튼 클래스
        /// </summary>
        public class CreateWorldObjectButton
        {
            public CreateWorldObjectButton(int newWorldObjectID)
            {
                newWorldObject = ScriptableObject.CreateInstance<WorldObjectData>();
                newWorldObject.SetWorldObjectID(newWorldObjectID);
            }

            [InlineEditor(objectFieldMode: InlineEditorObjectFieldModes.Hidden)]
            public WorldObjectData newWorldObject;

            [Button("월드 오브젝트 생성", ButtonSizes.Large)]
            [GUIColor(0.4f, 0.8f, 1f)]
            private void OnCreateWorldObjectBtnClicked()
            {
                OnCreateWorldObject?.Invoke(newWorldObject);
            }

            public Action<WorldObjectData> OnCreateWorldObject;
        }


        /// <summary>
        /// 아이템 데이터베이스 윈도우에서 재료 SO를 생성하는 메서드
        /// </summary>
        /// <param name="material"></param>
        private void CreateMaterial(MaterialData material)
        {
            string folderPath = materialFolderPath;
            int createdMaterialID = material.materialID;
            string fileName = $"{createdMaterialID}.asset";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string path = Path.Combine(folderPath, fileName);

            // 중복된 이름의 파일이 이미 존재하는지 체크
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("생성 실패", $"이미 동일한 이름의 에셋이 존재합니다:\n{fileName}", "확인");
                return;
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            lastMaterialID = createdMaterialID + 1;

            // ForceMenuTreeRebuild();
            // EditorUtility.FocusProjectWindow();
            // Selection.activeObject = material;
        }

        /// <summary>
        /// 아이템 데이터베이스 윈도우에서 포션 SO를 생성하는 메서드
        /// </summary>
        /// <param name="potion"></param>
        private void CreatePotion(PotionData potion)
        {
            string folderPath = potionFolderPath;
            int createdPotionID = potion.potionID;
            string fileName = $"{createdPotionID}.asset";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string path = Path.Combine(folderPath, fileName);

            // 중복된 이름의 파일이 이미 존재하는지 체크
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("생성 실패", $"이미 동일한 이름의 에셋이 존재합니다:\n{fileName}", "확인");
                return;
            }

            AssetDatabase.CreateAsset(potion, path);
            AssetDatabase.SaveAssets();

            lastPotionID = createdPotionID + 1;

            // ForceMenuTreeRebuild();
            // EditorUtility.FocusProjectWindow();
            // Selection.activeObject = potion;
        }


        /// <summary>
        /// 아이템 데이터베이스 윈도우에서 월드 오브젝트 SO를 생성하는 메서드
        /// </summary>
        /// <param name="worldObject"></param>
        private void CreateWorldObject(WorldObjectData worldObject)
        {
            string folderPath = worldObjectFolderPath;
            int createdWorldObjectID = worldObject.worldObjectID;
            string fileName = $"{createdWorldObjectID}.asset";

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string path = Path.Combine(folderPath, fileName);

            // 중복된 이름의 파일이 이미 존재하는지 체크
            if (File.Exists(path))
            {
                EditorUtility.DisplayDialog("생성 실패", $"이미 동일한 이름의 에셋이 존재합니다:\n{fileName}", "확인");
                return;
            }

            AssetDatabase.CreateAsset(worldObject, path);
            AssetDatabase.SaveAssets();

            lastWorldObjectID = createdWorldObjectID + 1;

            // ForceMenuTreeRebuild();
            // EditorUtility.FocusProjectWindow();
            // Selection.activeObject = worldObject;
        }
    }
}
#endif