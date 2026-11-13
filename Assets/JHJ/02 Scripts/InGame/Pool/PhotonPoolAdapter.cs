using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

public class PhotonPoolAdapter : IPunPrefabPool
{

    private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    private static readonly HashSet<string> pooledNames = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        PhotonNetwork.PrefabPool = new PhotonPoolAdapter();
    }


    public static void Register(GameObject prefab)
    {
        if (prefab == null)
            return;

        prefabs[prefab.name] = prefab;
    }

    public static void RegisterPooled(GameObject prefab)
    {
        if (prefab == null)
            return;

        Register(prefab);
        pooledNames.Add(prefab.name);
    }





    public void Destroy(GameObject gameObject)
    {
        if (gameObject.TryGetComponent(out PooledObject pooled) && PoolManager.Instance != null)
            PoolManager.Instance.Release(gameObject);
        else
            Object.Destroy(gameObject);
    }

    public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
    {
        // 등록된 곳에 있는지부터 확인, 없으면 Resources에서 찾아서 저장
        if(!prefabs.TryGetValue(prefabId, out GameObject prefab))
        {
            prefab = Resources.Load<GameObject>(prefabId);
            if (prefab != null)
                prefabs[prefabId] = prefab;
        }

        // 진짜 없다면?
        if(prefab == null)
        {
            Debug.LogError($"[PhotonPoolAdapter] 프리팹 없음: {prefabId} (Register했는지, Resources에 있는지 확인 필요)");
            return null;
        }

        // 풀 대상 -> PoolManager에서 꺼진 채로 꺼내기 (켜거나 viewId는 포톤)
        if (pooledNames.Contains(prefabId) && PoolManager.Instance != null)
            return PoolManager.Instance.Rent(prefab, position, rotation);

        // 풀 대상이 아니라면 -> 꺼진채로 생성, 프리팹을 잠시 끄고 복제해야 복제본이 꺼진채로 옴
#if UNITY_EDITOR
        bool wasDirty = UnityEditor.EditorUtility.IsDirty(prefab);   // 끄기 전에 원래 수정 상태 기억
#endif
        bool wasActive = prefab.activeSelf;
        if (wasActive)
            prefab.SetActive(false);

        GameObject instance = Object.Instantiate(prefab, position, rotation);

        if (wasActive)
            prefab.SetActive(true);
#if UNITY_EDITOR
        if (!wasDirty)
            UnityEditor.EditorUtility.ClearDirty(prefab);   // 원래 깨끗했으면 다시 깨끗하게
#endif

        return instance;
    }   // ← Instantiate 함수 끝
}
