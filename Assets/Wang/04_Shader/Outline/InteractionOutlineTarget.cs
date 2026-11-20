using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractionOutlineTarget : MonoBehaviour
{
    public static InteractionOutlineTarget Instance { get; private set; }
    public static Camera SelectedCamera { get; private set; }

    [Tooltip("비우면 자식 메시를 수집한다. 부모의 메시를 그리려면 직접 지정한다.")]
    [SerializeField] private Renderer[] outlineRenderers;
    [SerializeField] private Color outlineColor = Color.red;
    [SerializeField, Range(1, 4)] private int outlineWidth = 2;

    public Color OutlineColor => outlineColor;
    public int OutlineWidth => outlineWidth;

    public readonly struct Entry
    {
        public readonly Renderer Renderer;
        public readonly int SubMeshCount;

        public Entry(Renderer renderer, int subMeshCount)
        {
            Renderer = renderer;
            SubMeshCount = subMeshCount;
        }
    }

    private Entry[] entries;
    public Entry[] Entries
    {
        get
        {
            if (entries == null)
                CacheRenderers();
            return entries;
        }
    }

    private void Awake()
    {
        if (entries == null)
            CacheRenderers();
    }

    public static void Select(InteractionOutlineTarget target, Camera camera)
    {
        Instance = target != null && target.isActiveAndEnabled ? target : null;
        SelectedCamera = Instance != null ? camera : null;
    }

    private void OnDisable()
    {
        // 파괴되거나 풀에 반환된 오브젝트의 선택을 해제한다.
        if (Instance == this)
            Select(null, null);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection()
    {
        Instance = null;
        SelectedCamera = null;
    }

    private void CacheRenderers()
    {
        if (outlineRenderers == null || outlineRenderers.Length == 0)
            outlineRenderers = GetComponentsInChildren<Renderer>(true);

        var result = new List<Entry>(outlineRenderers.Length);
        foreach (Renderer targetRenderer in outlineRenderers)
        {
            if (targetRenderer == null)
                continue;

            Mesh mesh = null;
            if (targetRenderer is SkinnedMeshRenderer skinned)
                mesh = skinned.sharedMesh;
            else if (targetRenderer is MeshRenderer &&
                targetRenderer.TryGetComponent(out MeshFilter filter))
                mesh = filter.sharedMesh;

            // 파티클/라인 렌더러는 제외하고 서브메시 개수를 한 번만 캐시한다.
            if (mesh != null)
                result.Add(new Entry(targetRenderer, mesh.subMeshCount));
        }
        entries = result.ToArray();
    }
}
