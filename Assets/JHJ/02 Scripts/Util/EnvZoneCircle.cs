using Enzeun.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

public class EnvZoneCircle : MonoBehaviour
{
    [SerializeField, LabelText("온도")] private TemperatureEnvironment temperature = TemperatureEnvironment.Normal;
    [SerializeField, LabelText("우선순위")] private int priority = 1;
    [SerializeField, LabelText("반경 (m)"), MinValue(1f)] private float radius = 260f;
    [SerializeField, LabelText("박스 개수"), Range(1, 16)] private int boxCount = 16;
    [SerializeField, LabelText("높이")] private float height = 180f;
    [SerializeField, LabelText("중심 높이")] private float centerY = 40f;


#if UNITY_EDITOR
    [Button("박스 다시 만들기")]
    private void Rebuild()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        float side = radius * Mathf.Sqrt(2f);

        for (int i = 0; i < boxCount; i++)
        {
            var box = new GameObject($"Env_{temperature}_{i}");
            box.transform.SetParent(transform, false);
            box.transform.localRotation = Quaternion.Euler(0f, 90f / boxCount * i, 0f);

            var zone = box.AddComponent<EnvironmentZone>();
            var so = new UnityEditor.SerializedObject(zone);
            so.FindProperty("_overrideTemperature").boolValue = true;
            so.FindProperty("_temperature").enumValueIndex = (int)temperature;
            so.FindProperty("_priority").intValue = priority;
            so.FindProperty("_center").vector3Value = new Vector3(0f, centerY, 0f);
            so.FindProperty("_size").vector3Value = new Vector3(side, height, side);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
