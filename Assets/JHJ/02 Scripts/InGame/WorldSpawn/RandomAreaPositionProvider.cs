using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class RandomAreaPositionProvider : MonoBehaviour, ISpawnPositionProvider
{
    [SerializeField] private string areaName = "Walkable";   // NavMesh Area 이름
    [SerializeField] private int maxAttempts = 10;
    [SerializeField] private float sampleRadius = 1.0f;      // NavMesh에서 찾을 반경
    [SerializeField] private float minSpacing = 2.0f;        // 다른 오브젝트와의 최소거리
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask blockMask;            // 겹치면 안되는 것..
    [SerializeField] private bool randomYaw = true;

    private BoxCollider box;
    private int areaMask;


    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;

        int area = NavMesh.GetAreaFromName(areaName);

        if (area < 0)
        {
            Debug.LogWarning($"[RandomAreaPositionProvider] {name}: NavMesh Area '{areaName}'를 찾을 수 없어 모든 Area를 사용합니다.");
            areaMask = NavMesh.AllAreas;
        }
        else
            areaMask = 1 << area;   // areaMask는 비트마스크라서 area는 Area 번호(정수), 마스크는 그 번호 칸만 켠 값 → 1을 번호만큼 밀어서 변환
    }

    public bool TryGetPosition(out Vector3 position, out Quaternion rotation)
    {
        Bounds bounds = box.bounds;

        for(int i = 0; i < maxAttempts; i++)
        {
            // 박스 안 랜덤 x, z
            Vector3 origin = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                bounds.max.y,
                Random.Range(bounds.min.z, bounds.max.z));

            // 아래로 레이캐스트 쏴서 땅 높이 찾기
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit groundHit,
                bounds.size.y, groundMask, QueryTriggerInteraction.Ignore))
                continue;

            // area의 네비메시 위가 맞는지
            if (!NavMesh.SamplePosition(groundHit.point, out NavMeshHit navHit, sampleRadius, areaMask))
                continue;

            Vector3 point = navHit.position;

            // 주변에 막는게 있나
            if (Physics.CheckSphere(point, minSpacing, blockMask, QueryTriggerInteraction.Ignore))
                continue;

            // 플레이어 거리 검사? 

            // 성공시
            position = point;
            rotation = randomYaw ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) : Quaternion.identity;
            return true;
        }

        // 전부 실패하면?
        position = default;
        rotation = Quaternion.identity;
        return false;
    }

}
