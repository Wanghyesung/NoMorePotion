using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트(꽃, 나무, 광물 등)의 남은 횟수를 팀 전체가 똑같이 보도록 동기화하는 매니저입니다. 씬에 한 개 배치합니다. (PhotonView 필요)
    /// 규칙은 WorldObjectNetState / WorldObjectRules 가 맡고, 이 클래스는 메시지를 주고받고 씬의 WorldObject 에 결과를 전달합니다.
    /// 요청 -> 마스터 검증 -> 전원 적용 흐름은 InventoryNetworkSyncManager / TeamInventoryState 와 같습니다. (드롭은 마스터가 한 번만 굴림)
    /// 네트워크 번호가 0 이거나 방에 없으면 WorldObject 는 혼자(로컬) 동작합니다.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public sealed class WorldObjectNetworkSyncManager : MonoBehaviourPunCallbacks
    {
        public static WorldObjectNetworkSyncManager Instance { get; private set; }

        [SerializeField, LabelText("요청 제한 시간 (초)"), MinValue(0.5f)]
        private float _requestTimeoutSeconds = 5f;

        private const float ResyncCooldownSeconds = 1f;

        // 한 오브젝트에서 여러 재료 주머니가 나올 때 오브젝트 중심에서 떨어뜨려 놓는 거리입니다.
        [SerializeField, LabelText("주머니를 흩어 놓는 거리 (m)"), MinValue(0f)]
        private float _pouchScatterRadius = 0.7f;

        private readonly WorldObjectNetState _state = new WorldObjectNetState();
        private readonly Dictionary<int, WorldObject> _objects = new Dictionary<int, WorldObject>();
        private readonly Dictionary<int, UniTaskCompletionSource<WorldObjectResult>> _pending = new Dictionary<int, UniTaskCompletionSource<WorldObjectResult>>();
        private int _nextRequestId = 1;
        private float _lastResyncRequestTime = float.NegativeInfinity;

        public WorldObjectNetState State => _state;

        /// <summary>
        /// 월드 오브젝트가 소진되어 재료가 떨어질 때 (월드 오브젝트, 드롭 목록)으로 발생합니다. 모든 클라이언트에서 같은 값으로 발생합니다.
        /// 바닥 아이템(MaterialItem)을 만드는 쪽이 구독합니다. 드롭 번호(DropId)는 마스터가 발급한 고유 번호입니다.
        /// </summary>
        public event Action<WorldObject, WorldDrop[]> OnDropped;

        [ShowInInspector, ReadOnly, LabelText("등록된 오브젝트 수"), PropertyOrder(-3)]
        private int DebugRegisteredCount => _objects.Count;

        [ShowInInspector, ReadOnly, LabelText("사본 버전"), PropertyOrder(-2)]
        private int DebugVersion => _state.Version;

        [ShowInInspector, ReadOnly, LabelText("응답 대기 중인 요청"), PropertyOrder(-1)]
        private int DebugPendingCount => _pending.Count;

        /// <summary>
        /// 멀티플레이 중이라 요청을 보낼 수 있는 상태인지 여부입니다. WorldObject 가 로컬로 처리할지 요청할지 정하는 데 씁니다.
        /// </summary>
        public static bool IsActive => Instance != null && PhotonNetwork.InRoom;

        /// <summary>
        /// 싱글턴 등록 후 사본 이벤트를 구독합니다. 중복 매니저는 제거합니다.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[WorldObjectNetworkSyncManager]: 씬에 매니저가 두 개 이상 있어 새로 생긴 것을 제거합니다.");
                Destroy(this);
                return;
            }

            Instance = this;
            _state.OnChanged += HandleChanged;
            _state.OnDropped += HandleDropped;
        }

        /// <summary>
        /// 씬의 WorldObject 를 모두 등록하고, 이미 방 안이면 전체 목록을 요청합니다.
        /// </summary>
        private void Start()
        {
            // 중복으로 만들어져 제거될 매니저는 아무것도 하지 않습니다. (같은 번호를 두 번 등록하는 것을 막음)
            if (Instance != this)
            {
                return;
            }

            // 모든 WorldObject 의 Awake 가 끝난 뒤이므로 씬의 오브젝트를 한꺼번에 등록합니다.
            WorldObject[] sceneObjects = FindObjectsByType<WorldObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < sceneObjects.Length; i++)
            {
                Register(sceneObjects[i]);
            }

            if (PhotonNetwork.InRoom)
            {
                SendReady();
            }
        }

        /// <summary>
        /// 방에 들어오면 마스터에게 전체 목록을 요청합니다.
        /// </summary>
        public override void OnJoinedRoom()
        {
            if (Instance != this)
            {
                return;
            }

            SendReady();
        }

        /// <summary>
        /// 이벤트 구독을 해제하고, 응답 대기 중인 요청은 NotInRoom 실패로 끝냅니다.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                _state.OnChanged -= HandleChanged;
                _state.OnDropped -= HandleDropped;
            }

            foreach (KeyValuePair<int, UniTaskCompletionSource<WorldObjectResult>> entry in _pending)
            {
                entry.Value.TrySetResult(WorldObjectResult.Fail(WorldObjectDenyReason.NotInRoom));
            }

            _pending.Clear();
        }

        /// <summary>
        /// 월드 오브젝트를 등록합니다. 씬에 있는 것은 Start 에서 자동으로 등록되며, 나중에 만든 오브젝트는 직접 호출합니다.
        /// 네트워크 번호가 0 이면 로컬 전용이라 등록하지 않습니다.
        /// </summary>
        public bool Register(WorldObject worldObject)
        {
            if (worldObject == null || worldObject.NetworkId <= 0)
            {
                return false;
            }

            if (!_state.Register(worldObject.NetworkId, worldObject.data))
            {
                Debug.LogWarning($"[WorldObjectNetworkSyncManager]: {worldObject.name} 의 네트워크 번호({worldObject.NetworkId})를 등록하지 못했습니다. 번호가 겹치거나 데이터가 없습니다.");
                return false;
            }

            _objects.Add(worldObject.NetworkId, worldObject);
            return true;
        }

        // ==================== 요청 (어느 클라이언트에서나 호출) ============================

        /// <summary>
        /// 월드 오브젝트에 상호작용을 요청합니다. 상호작용 타입이 아니면 마스터가 NoEffect 로 거부합니다.
        /// </summary>
        public UniTask<WorldObjectResult> InteractAsync(int networkId)
        {
            return SendRequestAsync(new WorldObjectCommand { NetworkId = networkId, Kind = WorldObjectHitKind.Interact });
        }

        /// <summary>
        /// 포션의 월드 오브젝트 대미지를 요청합니다. targetMask 는 WorldObjectRules.ToTargetMask 로 만듭니다.
        /// </summary>
        public UniTask<WorldObjectResult> PotionDamageAsync(int networkId, int damage, int targetMask)
        {
            if (damage <= 0 || damage > ushort.MaxValue)
            {
                return UniTask.FromResult(WorldObjectResult.Fail(WorldObjectDenyReason.Invalid));
            }

            return SendRequestAsync(new WorldObjectCommand
            {
                NetworkId = networkId,
                Kind = WorldObjectHitKind.PotionDamage,
                Damage = damage,
                TargetMask = targetMask,
            });
        }

        private async UniTask<WorldObjectResult> SendRequestAsync(WorldObjectCommand request)
        {
            if (!PhotonNetwork.InRoom)
            {
                return WorldObjectResult.Fail(WorldObjectDenyReason.NotInRoom);
            }

            request.RequestId = _nextRequestId++;

            var source = new UniTaskCompletionSource<WorldObjectResult>();
            _pending[request.RequestId] = source;

            // 내가 마스터이면 이 호출 안에서 바로 처리되어 결과가 이미 채워져 있을 수 있습니다.
            photonView.RPC(nameof(RpcRequest), RpcTarget.MasterClient, WorldObjectMessages.PackRequest(request));

            (bool isDone, WorldObjectResult result) = await UniTask.WhenAny(
                source.Task,
                UniTask.Delay(TimeSpan.FromSeconds(_requestTimeoutSeconds), ignoreTimeScale: true));

            if (isDone)
            {
                return result;
            }

            _pending.Remove(request.RequestId);
            Debug.LogWarning($"[WorldObjectNetworkSyncManager]: 요청({request.RequestId})의 응답이 {_requestTimeoutSeconds}초 안에 오지 않았습니다.");
            return WorldObjectResult.Fail(WorldObjectDenyReason.Timeout);
        }

        /// <summary>
        /// 기다리던 요청을 결과와 함께 끝냅니다. 이미 끝났거나 없는 요청이면 무시합니다.
        /// </summary>
        private void Complete(int requestId, WorldObjectResult result)
        {
            if (_pending.TryGetValue(requestId, out UniTaskCompletionSource<WorldObjectResult> source))
            {
                _pending.Remove(requestId);
                source.TrySetResult(result);
            }
        }

        // ==================== 사본 변화 -> 씬 오브젝트 ============================

        /// <summary>
        /// 사본의 남은 횟수가 바뀌면 해당 씬 오브젝트에 반영합니다.
        /// </summary>
        private void HandleChanged(int networkId, int previous, int remaining)
        {
            if (_objects.TryGetValue(networkId, out WorldObject worldObject) && worldObject != null)
            {
                worldObject.ApplyNetworkCount(remaining);
            }
        }

        /// <summary>
        /// 사본에서 드롭이 확정되면 해당 씬 오브젝트와 함께 OnDropped 로 알립니다.
        /// </summary>
        private void HandleDropped(int networkId, WorldDrop[] drops)
        {
            if (_objects.TryGetValue(networkId, out WorldObject worldObject) && worldObject != null)
            {
                OnDropped?.Invoke(worldObject, drops);

                // 바닥 주머니는 마스터가 한 번만 만들어 모든 클라이언트에 알립니다. (주우면 인벤토리 상태에서 한 번에 옮겨지므로 같은 곳에서 관리)
                if (PhotonNetwork.IsMasterClient)
                {
                    SpawnGroundPouches(worldObject.transform.position, drops);
                }
            }
        }

        /// <summary>
        /// 소진된 월드 오브젝트의 드롭(재료 종류별 개수)을 바닥 주머니로 만듭니다. 한 종류당 주머니 한 개이고, 주머니는 오브젝트 주위에 흩어 놓습니다.
        /// 위치는 마스터가 정해서 모든 클라이언트에 같은 값으로 전달됩니다. 땅이 보이면 땅 위에 놓고, 아니면 오브젝트와 같은 높이에 놓습니다.
        /// </summary>
        private void SpawnGroundPouches(Vector3 center, WorldDrop[] drops)
        {
            if (drops == null || drops.Length == 0 || InventoryNetworkSyncManager.Instance == null)
            {
                return;
            }

            var spawns = new List<GroundSpawn>(drops.Length);

            for (int i = 0; i < drops.Length; i++)
            {
                Vector3 position = center;

                if (drops.Length > 1)
                {
                    float angle = 360f * i / drops.Length;
                    position += Quaternion.Euler(0f, angle, 0f) * Vector3.forward * _pouchScatterRadius;
                }

                // 위에서 아래로 쏘아 땅 높이를 찾습니다. (시작점이 오브젝트 안에 파묻히지 않도록 조금 위에서)
                if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point;
                }

                spawns.Add(new GroundSpawn { MaterialId = drops[i].MaterialId, Count = drops[i].Count, Position = position });
            }

            InventoryNetworkSyncManager.Instance.SpawnGroundDrops(spawns);
        }

        // ==================== 네트워크 메시지 (RPC) ============================

        /// <summary>
        /// [마스터] 요청을 검증합니다. 통과하면 전원에게 적용을, 거부하면 요청한 사람에게만 이유를 보냅니다.
        /// </summary>
        [PunRPC]
        public void RpcRequest(byte[] data, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient || info.Sender == null)
            {
                return;
            }

            if (!WorldObjectMessages.TryUnpackRequest(data, out WorldObjectCommand request))
            {
                if (WorldObjectMessages.TryPeekRequestId(data, out int brokenRequestId))
                {
                    photonView.RPC(nameof(RpcDenied), info.Sender, WorldObjectMessages.PackDeny(brokenRequestId, WorldObjectDenyReason.Invalid));
                }

                Debug.LogWarning("[WorldObjectNetworkSyncManager]: 깨진 월드 오브젝트 요청을 받아 무시했습니다.");
                return;
            }

            if (!_state.TryValidate(request, info.Sender.ActorNumber, out WorldObjectCommand apply, out WorldObjectDenyReason reason))
            {
                photonView.RPC(nameof(RpcDenied), info.Sender, WorldObjectMessages.PackDeny(request.RequestId, reason));
                return;
            }

            photonView.RPC(nameof(RpcApply), RpcTarget.All, WorldObjectMessages.PackApply(apply));
        }

        /// <summary>
        /// [전원] 마스터가 확정한 변경을 사본에 적용합니다. 요청한 사람이면 기다리던 요청을 끝냅니다.
        /// </summary>
        [PunRPC]
        public void RpcApply(byte[] data, PhotonMessageInfo info)
        {
            if (!IsFromMaster(info))
            {
                return;
            }

            if (!WorldObjectMessages.TryUnpackApply(data, out WorldObjectCommand apply))
            {
                Debug.LogWarning("[WorldObjectNetworkSyncManager]: 깨진 적용 메시지를 받아 전체 목록을 다시 요청합니다.");
                RequestResync();
                return;
            }

            WorldObjectApplyStatus status = _state.Apply(apply, out WorldObjectResult result);

            switch (status)
            {
                case WorldObjectApplyStatus.Applied:
                    if (IsLocalActor(apply.RequesterActor))
                    {
                        Complete(apply.RequestId, result);
                    }
                    break;

                case WorldObjectApplyStatus.Ignored:
                case WorldObjectApplyStatus.VersionGap:
                case WorldObjectApplyStatus.Inconsistent:
                    // 내 요청의 적용인데 사본에 반영하지 못했다면 결과를 알 수 없습니다. 제한 시간까지 기다리지 않고 바로 알립니다.
                    if (IsLocalActor(apply.RequesterActor))
                    {
                        Complete(apply.RequestId, WorldObjectResult.Fail(WorldObjectDenyReason.OutOfSync));
                    }

                    // Ignored 는 이미 반영된 오래된 메시지라 전체 목록을 다시 받을 필요는 없습니다.
                    if (status != WorldObjectApplyStatus.Ignored)
                    {
                        RequestResync();
                    }
                    break;
            }
        }

        /// <summary>
        /// [요청한 사람] 마스터의 거부를 받아 기다리던 요청을 실패로 끝냅니다.
        /// </summary>
        [PunRPC]
        public void RpcDenied(byte[] data, PhotonMessageInfo info)
        {
            if (!IsFromMaster(info))
            {
                return;
            }

            if (WorldObjectMessages.TryUnpackDeny(data, out int requestId, out WorldObjectDenyReason reason))
            {
                Complete(requestId, WorldObjectResult.Fail(reason));
            }
        }

        /// <summary>
        /// [마스터] 씬 로딩이 끝났거나 사본이 어긋난 클라이언트에게 전체 목록을 보냅니다.
        /// </summary>
        [PunRPC]
        public void RpcReady(PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient || info.Sender == null || info.Sender.IsLocal)
            {
                return;
            }

            photonView.RPC(nameof(RpcSnapshot), info.Sender, _state.CreateSnapshot());
        }

        /// <summary>
        /// [클라이언트] 마스터가 보낸 전체 목록으로 사본을 통째로 바꿉니다.
        /// </summary>
        [PunRPC]
        public void RpcSnapshot(byte[] data, PhotonMessageInfo info)
        {
            if (!IsFromMaster(info) || PhotonNetwork.IsMasterClient)
            {
                return;
            }

            if (!_state.TryLoadSnapshot(data))
            {
                Debug.LogWarning("[WorldObjectNetworkSyncManager]: 전체 목록을 풀지 못했습니다. 기존 사본을 유지합니다.");
            }
        }

        // ==================== 내부 ============================

        private static bool IsFromMaster(PhotonMessageInfo info)
        {
            return info.Sender != null && info.Sender.IsMasterClient;
        }

        private static bool IsLocalActor(int actorNumber)
        {
            return PhotonNetwork.LocalPlayer != null && actorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
        }

        private void SendReady()
        {
            photonView.RPC(nameof(RpcReady), RpcTarget.MasterClient);
        }

        private void RequestResync()
        {
            if (PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            {
                return;
            }

            if (Time.unscaledTime - _lastResyncRequestTime < ResyncCooldownSeconds)
            {
                return;
            }

            _lastResyncRequestTime = Time.unscaledTime;
            SendReady();
        }
    }
}
