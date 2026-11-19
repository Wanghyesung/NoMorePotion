using Sirenix.OdinInspector;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class NavAreaCircle : MonoBehaviour
{
    [SerializeField, LabelText("Area 이름")] private string areaName = "Plain";
    [SerializeField, LabelText("반경 (m)"), MinValue(1f)] private float radius = 260f;
    [SerializeField, LabelText("박스 개수"), Range(1, 16)] private int boxCount = 8;
    [SerializeField, LabelText("높이")] private float height = 180f;
    [SerializeField, LabelText("중심 높이")] private float centerY = 40f;


    [Button("박스 다시 만들기")]
    private void Rebuild()
    {
        int area = NavMesh.GetAreaFromName(areaName);

        if(area < 0)
        {
            Debug.LogError("[NavMeshCircle] Area '{areaName}'를 찾을 수 없음", this);
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        for (int i =0; i < boxCount; i++)
        {
            GameObject box = new GameObject($"{areaName}_{i}");
            box.transform.SetParent(transform, false);
            box.transform.localRotation = Quaternion.Euler(0f, 90f / boxCount * i, 0f);

            NavMeshModifierVolume volume = box.AddComponent<NavMeshModifierVolume>();
            volume.area = area;
            float side = radius * Mathf.Sqrt(2f);
            volume.size = new Vector3(side, height, side);
            volume.center = new Vector3(0f, centerY, 0f);
        }
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
