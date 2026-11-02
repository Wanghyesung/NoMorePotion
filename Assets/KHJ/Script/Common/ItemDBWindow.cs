#if UNITY_EDITOR
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using System.IO;

using Enzeun.SO;
using System;

namespace Enzeun.Editor
{

    public class ItemDatabaseWindow : OdinMenuEditorWindow
    {
        // --- 입력 필드 변수들 ---
        [SerializeField, LabelText("재료 ID"), HideLabel]
        private int inputMaterialID = 20001;

        [SerializeField, LabelText("포션 ID"), HideLabel]
        private int inputPotionID = 10001;

        CreateMaterialButton createMaterialButton;
        CreatePotionButton createPotionButton;

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
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();

            createMaterialButton = new CreateMaterialButton();
            createMaterialButton.OnCreateMaterial += CreateMaterial;
            tree.Add("Material SO 생성", createMaterialButton);

            createPotionButton = new CreatePotionButton();
            createPotionButton.OnCreatePotion += CreatePotion;
            tree.Add("Potion SO 생성", createPotionButton);

            tree.AddAllAssetsAtPath("재료 목록", "Assets/KHJ/ScriptableObjects/Material", typeof(MaterialData), true);
            tree.AddAllAssetsAtPath("포션 목록", "Assets/KHJ/ScriptableObjects/Potion", typeof(PotionData), true);

            return tree;
        }

        public class CreateMaterialButton
        {
            public CreateMaterialButton()
            {
                newMaterial = ScriptableObject.CreateInstance<MaterialData>();
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

        public class CreatePotionButton
        {
            public CreatePotionButton()
            {
                newPotion = ScriptableObject.CreateInstance<PotionData>();
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

        private void CreateMaterial(MaterialData material)
        {
            string folderPath = "Assets/KHJ/ScriptableObjects/Material";
            string fileName = $"{material.GetMaterialId()}.asset";

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
        }

        private void CreatePotion(PotionData potion)
        {
            string folderPath = "Assets/KHJ/ScriptableObjects/Potion";
            string fileName = $"{potion.GetPotionId()}.asset";

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
        }
    }
}
#endif