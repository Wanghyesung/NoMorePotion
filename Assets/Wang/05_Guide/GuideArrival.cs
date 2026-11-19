using UnityEngine;

public sealed class GuideArrival : MonoBehaviour
{
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Transform spawnPoint;

    private GameObject portalInstance;

    private void OnEnable()
    {
        if (portalPrefab == null)
            return;

        portalInstance = Instantiate(portalPrefab, transform, false);
        if (spawnPoint != null)
            portalInstance.transform.SetPositionAndRotation(
                spawnPoint.position, spawnPoint.rotation * portalPrefab.transform.localRotation);
    }

    private void OnDisable()
    {
        if (portalInstance != null)
            Destroy(portalInstance);
        portalInstance = null;
    }
}
