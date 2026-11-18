using System.Collections.Generic;
using Enzeun.SO;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;
using Hashtable = ExitGames.Client.Photon.Hashtable;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 게임 씬이 시작될 때 지역별로 월드 오브젝트를 자동 배치합니다. 씬에 한 개 둡니다.
    /// 방장이 정한 시드(WorldSeed)로 모든 클라이언트가 같은 배치를 만들므로, 네트워크 번호가 모두에게 같은 오브젝트를 가리킵니다.
    ///
    /// 같은 결과가 나오게 하는 규칙 (어기면 클라이언트마다 배치가 달라져 소진 동기화가 어긋남)
    /// - UnityEngine.Random 을 쓰지 않고 시드로 만든 System.Random 만 씁니다. 지역마다 따로 만듭니다.
    /// - 지역은 "지역 목록"의 순서대로 돕니다. (FindObjects 순서에 의존하지 않음)
    /// - 위치 판정에는 씬에 고정된 것(지면 레이어, NavMesh, 배치 금지 구역)만 씁니다. 플레이어, 몬스터 같은 움직이는 것은 보지 않습니다.
    /// - 네트워크 번호 = 번호 시작값 + 지역 번호 x 지역당 번호 폭 + 뽑은 순서. 못 놓은 자리도 번호를 건너뛰므로 결과가 같으면 번호도 같습니다.
    ///
    /// 위치 판정 : 박스 윗면에서 아래로 쏴서 지면 레이어에 맞고, 경사가 한도 이하이고, NavMesh 위이고, 배치 금지 구역이 아니고, 이미 놓은 것과 반경 합 이상 떨어져 있어야 합니다.
    /// 판정에 실패해서 최대 시도 횟수를 넘으면 그 자리는 건너뜁니다. (모든 클라이언트가 같은 자리에서 건너뜀)
    ///
    /// 시드는 방장이 새 판을 시작할 때 WorldSeed.Roll() 로 정합니다. 시드가 아직 없으면 이 컴포넌트가 방장일 때 대신 정합니다. (테스트용)
    /// 오브젝트는 Awake 에서 만들어 WorldObjectNetworkSyncManager 의 Start 등록에 포함됩니다. 방에 늦게 들어와 그 뒤에 만들게 되면 직접 등록하고 전체 목록을 다시 요청합니다.
    /// </summary>
    public class WorldObjectAutoSpawner : MonoBehaviourPunCallbacks
    {
        #region Types
        /// <summary>
        /// 계산된 배치 한 건입니다. (만들기 전의 결과라서 미리보기에도 씁니다)
        /// </summary>
        public readonly struct Placement
        {
            public readonly int RegionIndex;
            public readonly int NetworkId;
            public readonly WorldObjectSpawnTable.Entry Entry;
            public readonly Vector3 Position;
            public readonly float Yaw;

            public Placement(int regionIndex, int networkId, WorldObjectSpawnTable.Entry entry, Vector3 position, float yaw)
            {
                RegionIndex = regionIndex;
                NetworkId = networkId;
                Entry = entry;
                Position = position;
                Yaw = yaw;
            }
        }
        #endregion

        #region Config
        [SerializeField, LabelText("지역 목록 (이 순서로 배치, 순서를 바꾸면 배치가 달라짐)"), BoxGroup("지역")]
        private List<WorldObjectSpawnRegion> _regions = new List<WorldObjectSpawnRegion>();

        [SerializeField, LabelText("만든 오브젝트를 모아 둘 부모 (선택)"), BoxGroup("지역")]
        private Transform _parent;

        [SerializeField, LabelText("지면 레이어"), BoxGroup("위치 판정")]
        private LayerMask _groundMask;

        [SerializeField, LabelText("경사 한도 (도)"), BoxGroup("위치 판정"), Range(0f, 89f)]
        private float _maxSlopeDegrees = 35f;

        [SerializeField, LabelText("NavMesh 영역 이름"), BoxGroup("위치 판정")]
        private string _navAreaName = "Walkable";

        [SerializeField, LabelText("NavMesh 탐색 반경 (m)"), BoxGroup("위치 판정"), MinValue(0.01f)]
        private float _navSampleRadius = 1f;

        [SerializeField, LabelText("자리마다 최대 시도 횟수"), BoxGroup("위치 판정"), MinValue(1)]
        private int _maxAttempts = 20;

        [SerializeField, LabelText("네트워크 번호 시작값"), BoxGroup("네트워크 번호"), MinValue(1)]
        private int _networkIdBase = 100000;

        [SerializeField, LabelText("지역당 번호 폭 (= 지역당 최대 개수 + 1)"), BoxGroup("네트워크 번호"), MinValue(2)]
        private int _networkIdStride = 1000;

        [SerializeField, LabelText("고정 시드 (0 = 무작위, 개발용)"), BoxGroup("개발용")]
        private int _debugFixedSeed;
        #endregion

        #region State
        private readonly List<WorldObject> _spawned = new List<WorldObject>();
        private bool _hasSpawned;

        [ShowInInspector, ReadOnly, LabelText("사용한 시드"), BoxGroup("개발용")]
        public int UsedSeed { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("만든 오브젝트 수"), BoxGroup("개발용")]
        private int DebugSpawnedCount => _spawned.Count;

        private List<Placement> _preview = new List<Placement>();
        #endregion

        #region Lifecycle
        // 새로 붙였을 때 지면 레이어를 Ground 로 맞춰 둡니다.
        private void Reset()
        {
            _groundMask = LayerMask.GetMask("Ground");
        }

        // WorldObjectNetworkSyncManager 의 Start 보다 먼저 끝내야 해서 Awake 에서 시도합니다.
        private void Awake()
        {
            TryBegin(false);
        }

        // 방이 아직 없을 때(테스트 씬 등) 들어오면 그때 시작합니다.
        public override void OnJoinedRoom()
        {
            TryBegin(true);
        }

        // 방장이 시드를 늦게 정한 경우를 기다립니다.
        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (propertiesThatChanged != null && propertiesThatChanged.ContainsKey(WorldSeed.RoomKey))
            {
                TryBegin(true);
            }
        }
        #endregion

        #region Begin
        private void TryBegin(bool late)
        {
            if (_hasSpawned || !PhotonNetwork.InRoom)
            {
                return;
            }

            int seed;

            if (PhotonNetwork.IsMasterClient && _debugFixedSeed != 0)
            {
                seed = WorldSeed.Roll(_debugFixedSeed);
            }
            else if (!WorldSeed.TryGet(out seed))
            {
                // 방장이 아니면 방장이 시드를 정하길 기다립니다. (OnRoomPropertiesUpdate)
                if (!PhotonNetwork.IsMasterClient)
                {
                    return;
                }

                seed = WorldSeed.Roll();
            }

            _hasSpawned = true;
            UsedSeed = seed;
            SpawnAll(seed, late);
        }

        private void SpawnAll(int seed, bool registerNow)
        {
            List<Placement> layout = BuildLayout(seed);

            for (int i = 0; i < layout.Count; i++)
            {
                Placement placement = layout[i];
                WorldObject worldObject = Instantiate(placement.Entry.Prefab, placement.Position, Quaternion.Euler(0f, placement.Yaw, 0f), _parent);
                worldObject.Initialize(placement.NetworkId);
                worldObject.name = $"{placement.Entry.Prefab.name}_{placement.NetworkId}";
                _spawned.Add(worldObject);
            }

#if UNITY_EDITOR
            Debug.Log($"[WorldObjectAutoSpawner] : 시드 {seed} 로 월드 오브젝트 {layout.Count}개를 배치했습니다.");
#endif

            if (registerNow)
            {
                RegisterLate();
            }
        }

        // 매니저의 Start 등록이 이미 끝난 뒤에 만든 경우입니다. 직접 등록하고, 클라이언트는 전체 목록(남은 횟수)을 다시 받습니다.
        private void RegisterLate()
        {
            WorldObjectNetworkSyncManager manager = WorldObjectNetworkSyncManager.Instance;
            if (manager == null)
            {
                return;
            }

            for (int i = 0; i < _spawned.Count; i++)
            {
                if (!manager.State.Contains(_spawned[i].NetworkId))
                {
                    manager.Register(_spawned[i]);
                }
            }

            manager.RequestFullSync();
        }
        #endregion

        #region Layout
        /// <summary>
        /// 시드로 전체 배치를 계산합니다. 오브젝트는 만들지 않습니다. 같은 시드와 같은 씬이면 항상 같은 결과입니다.
        /// </summary>
        public List<Placement> BuildLayout(int seed)
        {
            var result = new List<Placement>();
            var placed = new List<(Vector3 position, float radius)>();
            WorldObjectSpawnBlocker[] blockers = FindObjectsByType<WorldObjectSpawnBlocker>(FindObjectsSortMode.None);
            int areaMask = ResolveAreaMask();

            for (int r = 0; r < _regions.Count; r++)
            {
                WorldObjectSpawnRegion region = _regions[r];

                if (region == null || region.Table == null)
                {
                    Debug.LogWarning($"[WorldObjectAutoSpawner]: 지역 목록 {r}번이 비었거나 배치 테이블이 없어 건너뜁니다.", this);
                    continue;
                }

                int count = region.Count;
                if (count >= _networkIdStride)
                {
                    Debug.LogWarning($"[WorldObjectAutoSpawner]: {region.name} 의 개수({count})가 번호 폭({_networkIdStride})을 넘어 {_networkIdStride - 1}개로 줄입니다.", region);
                    count = _networkIdStride - 1;
                }

                var rng = new System.Random(MixSeed(seed, r));
                int failed = 0;

                for (int k = 0; k < count; k++)
                {
                    int entryIndex = region.Table.PickIndex(rng);
                    if (entryIndex < 0)
                    {
                        break;
                    }

                    WorldObjectSpawnTable.Entry entry = region.Table.Entries[entryIndex];

                    if (TryFindPosition(region, entry.Radius, rng, blockers, placed, areaMask, out Vector3 position))
                    {
                        float yaw = (float)rng.NextDouble() * 360f;
                        int networkId = _networkIdBase + r * _networkIdStride + k + 1;

                        result.Add(new Placement(r, networkId, entry, position, yaw));
                        placed.Add((position, entry.Radius));
                    }
                    else
                    {
                        failed++;
                    }
                }

                if (failed > 0)
                {
                    Debug.LogWarning($"[WorldObjectAutoSpawner]: {region.name} 에서 {failed}개를 놓을 자리를 찾지 못해 건너뛰었습니다. (박스를 키우거나 금지 구역, 간격을 줄여 보세요)", region);
                }
            }

            return result;
        }

        private bool TryFindPosition(WorldObjectSpawnRegion region, float radius, System.Random rng, WorldObjectSpawnBlocker[] blockers,
            List<(Vector3 position, float radius)> placed, int areaMask, out Vector3 position)
        {
            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                // 시도마다 같은 횟수만큼 뽑아야 하므로 판정 전에 먼저 뽑습니다.
                Vector3 origin = region.NextRayOrigin(rng);

                // 지면 레이어에 맞아야 합니다.
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, region.RayDistance, _groundMask, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                if (Vector3.Angle(hit.normal, Vector3.up) > _maxSlopeDegrees)
                {
                    continue;
                }

                // 걸어 다닐 수 있는 곳(NavMesh 위)이어야 합니다.
                if (!NavMesh.SamplePosition(hit.point, out NavMeshHit _, _navSampleRadius, areaMask))
                {
                    continue;
                }

                if (IsBlocked(hit.point, radius, blockers) || IsTooClose(hit.point, radius, placed))
                {
                    continue;
                }

                position = hit.point;
                return true;
            }

            position = default;
            return false;
        }

        private static bool IsBlocked(Vector3 point, float radius, WorldObjectSpawnBlocker[] blockers)
        {
            for (int i = 0; i < blockers.Length; i++)
            {
                if (blockers[i].Blocks(point, radius))
                {
                    return true;
                }
            }

            return false;
        }

        // 이미 놓은 것과 반경 합 이상 떨어져야 합니다. (수평 거리)
        private static bool IsTooClose(Vector3 point, float radius, List<(Vector3 position, float radius)> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                float dx = placed[i].position.x - point.x;
                float dz = placed[i].position.z - point.z;
                float limit = placed[i].radius + radius;

                if (dx * dx + dz * dz < limit * limit)
                {
                    return true;
                }
            }

            return false;
        }

        // 시드와 지역 번호를 섞어 지역별 시드를 만듭니다. (string.GetHashCode 같은 프로세스마다 달라지는 값은 쓰지 않음)
        private static int MixSeed(int seed, int regionIndex)
        {
            unchecked
            {
                int hash = seed;
                hash = hash * 16777619 ^ regionIndex;
                hash = hash * 16777619 ^ 0x5bd1e995;
                return hash;
            }
        }

        private int ResolveAreaMask()
        {
            int area = NavMesh.GetAreaFromName(_navAreaName);

            if (area < 0)
            {
                Debug.LogWarning($"[WorldObjectAutoSpawner]: NavMesh 영역 '{_navAreaName}' 를 찾을 수 없어 모든 영역을 씁니다.", this);
                return NavMesh.AllAreas;
            }

            return 1 << area;
        }
        #endregion

        #region Preview
        /// <summary>
        /// 시드를 넣고 배치를 계산해서 씬 뷰에 구로 보여 줍니다. (오브젝트는 만들지 않음, 이 오브젝트를 선택해야 보임)
        /// </summary>
        [Button("배치 미리보기 (씬 뷰에 표시)"), BoxGroup("개발용")]
        private void Preview(int seed = 12345)
        {
            Physics.SyncTransforms();
            _preview = BuildLayout(seed);
            Debug.Log($"[WorldObjectAutoSpawner] : 미리보기 시드 {seed} - {_preview.Count}개 (이 오브젝트를 선택하면 씬 뷰에 보입니다)");
        }

        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < _preview.Count; i++)
            {
                Placement placement = _preview[i];
                Gizmos.color = Color.HSVToRGB((placement.RegionIndex * 0.17f) % 1f, 0.8f, 1f);
                Gizmos.DrawWireSphere(placement.Position + Vector3.up * 0.1f, Mathf.Max(0.1f, placement.Entry.Radius));
            }
        }
        #endregion
    }
}
