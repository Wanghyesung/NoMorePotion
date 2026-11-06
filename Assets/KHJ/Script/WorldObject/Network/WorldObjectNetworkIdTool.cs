#if UNITY_EDITOR

using System.Collections.Generic;
using Enzeun.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Enzeun.Editor
{
    /// <summary>
    /// 열려 있는 씬의 WorldObject 에 네트워크 번호를 채우는 도구입니다.
    /// 번호가 0 이거나 다른 오브젝트와 겹치는 것만 새 번호(현재 최대값 + 1 부터)를 받고, 이미 고유한 번호는 그대로 둡니다.
    /// 번호는 씬에 저장되므로 모든 클라이언트가 같은 값을 갖습니다. 실행 후 씬을 저장해야 반영됩니다.
    /// </summary>
    public static class WorldObjectNetworkIdTool
    {
        [MenuItem("Enzeun/월드 오브젝트/네트워크 번호 자동 부여")]
        private static void AssignIds()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[WorldObjectNetworkIdTool]: Play 모드에서는 실행할 수 없습니다.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            var targets = new List<WorldObject>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                targets.AddRange(root.GetComponentsInChildren<WorldObject>(true));
            }

            // 계층 위치 순서로 번호를 받게 해서, 같은 씬이면 항상 같은 순서가 되게 합니다.
            targets.Sort((a, b) => string.CompareOrdinal(GetPath(a.transform), GetPath(b.transform)));

            var used = new HashSet<int>();
            int max = 0;
            var needs = new List<WorldObject>();

            for (int i = 0; i < targets.Count; i++)
            {
                int id = targets[i].NetworkId;

                if (id > 0 && used.Add(id))
                {
                    max = Mathf.Max(max, id);
                }
                else
                {
                    needs.Add(targets[i]);
                }
            }

            for (int i = 0; i < needs.Count; i++)
            {
                var serialized = new SerializedObject(needs[i]);
                serialized.FindProperty("_networkId").intValue = ++max;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(needs[i]);
            }

            if (needs.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log($"[WorldObjectNetworkIdTool] : 씬 '{scene.name}' 의 월드 오브젝트 {targets.Count}개 중 {needs.Count}개에 번호를 새로 부여했습니다. 씬을 저장해 주세요.");
        }

        /// <summary>
        /// 루트부터 "이름#형제 순서" 를 이어 붙인 계층 경로를 만듭니다. 번호 부여 순서를 고정하는 정렬 키로 씁니다.
        /// </summary>
        private static string GetPath(Transform transform)
        {
            string path = transform.name + "#" + transform.GetSiblingIndex();

            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "#" + transform.GetSiblingIndex() + "/" + path;
            }

            return path;
        }
    }
}

#endif
