using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 팀이 공유하는 인벤토리(재료, 포션)와 시체 상자를 네트워크로 동기화하는 매니저입니다. 씬에 한 개 배치합니다. (PhotonView 필요)
    /// 실제 규칙(검증, 적용, 전체 목록)은 네트워크를 모르는 TeamInventoryState 가 맡고, 이 클래스는 메시지를 주고받는 일만 합니다.
    ///
    /// 흐름
    /// 1) 아무 클라이언트나 *Async 메서드로 요청하면, 요청 번호를 붙여 마스터에게 보냅니다. (RpcRequest)
    /// 2) 마스터가 검증합니다. 거부하면 요청한 사람에게만 알리고(RpcDenied), 통과하면 확정한 값을 전원에게 보냅니다. (RpcApply)
    /// 3) 모든 클라이언트(마스터 포함)가 같은 값을 자기 사본에 적용합니다. 요청한 사람의 *Async 는 이때 결과를 돌려받고 끝납니다.
    /// 씬 로딩이 끝난 클라이언트는 마스터에게 "준비됐다"고 알리고(RpcReady), 마스터가 전체 목록을 한 번 보냅니다. (RpcSnapshot)
    /// 중간 메시지를 놓치거나 사본이 어긋나면 같은 방법으로 전체 목록을 다시 받습니다.
    ///
    /// 오프라인 모드에서는 내가 마스터이므로 같은 코드가 그대로 동작합니다.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public sealed class InventoryNetworkSyncManager : MonoBehaviourPunCallbacks
    {
        public static InventoryNetworkSyncManager Instance { get; private set; }

        [SerializeField, LabelText("요청 제한 시간 (초)"), MinValue(0.5f)]
        private float _requestTimeoutSeconds = 5f;

        // 전체 목록을 다시 요청하는 최소 간격입니다. (어긋난 상태에서 메시지가 연달아 와도 요청을 쏟아내지 않기 위함)
        private const float ResyncCooldownSeconds = 1f;

        private TeamInventoryState _state;
        private readonly Dictionary<int, UniTaskCompletionSource<InventoryResult>> _pending = new Dictionary<int, UniTaskCompletionSource<InventoryResult>>();
        private int _nextRequestId = 1;
        private float _lastResyncRequestTime = float.NegativeInfinity;

        /// <summary>
        /// 팀 공유 인벤토리 상태의 사본입니다. 읽기와 이벤트 구독에 사용하고, 바꾸는 것은 *Async 요청으로만 합니다.
        /// </summary>
        public TeamInventoryState State => _state;

        public MaterialInventory TeamMaterials => _state.Materials;
        public PotionInventory TeamPotions => _state.Potions;

        [ShowInInspector, ReadOnly, LabelText("사본 버전"), PropertyOrder(-2)]
        private int DebugVersion => _state != null ? _state.Version : 0;

        [ShowInInspector, ReadOnly, LabelText("응답 대기 중인 요청"), PropertyOrder(-1)]
        private int DebugPendingCount => _pending.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[InventoryNetworkSyncManager]: 씬에 매니저가 두 개 이상 있어 새로 생긴 것을 제거합니다.");
                Destroy(this);
                return;
            }

            Instance = this;
            _state = new TeamInventoryState();
        }

        private void Start()
        {
            // 이미 방에 있는 상태로 씬이 로딩되었으면 바로 "준비됐다"고 알립니다.
            if (PhotonNetwork.InRoom)
            {
                SendReady();
            }
        }

        public override void OnJoinedRoom()
        {
            SendReady();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // 기다리던 요청이 영원히 끝나지 않지 않도록 실패로 마무리합니다.
            foreach (KeyValuePair<int, UniTaskCompletionSource<InventoryResult>> entry in _pending)
            {
                entry.Value.TrySetResult(InventoryResult.Fail(InventoryDenyReason.NotInRoom));
            }

            _pending.Clear();
        }

        // ==================== 요청 (어느 클라이언트에서나 호출) ============================

        /// <summary>
        /// 팀 재료 인벤토리에 재료를 넣습니다. 한도를 넘으면 들어간 만큼만 넣고, Outcome.Value 에 실제로 넣은 개수가 담깁니다.
        /// </summary>
        public UniTask<InventoryResult> AddMaterialAsync(int materialId, int count)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.AddMaterial, MaterialId = materialId, Count = count });
        }

        /// <summary>
        /// 팀 재료 인벤토리에서 재료를 뺍니다. 개수가 모자라면 아무것도 빼지 않고 NotEnough 로 실패합니다.
        /// </summary>
        public UniTask<InventoryResult> RemoveMaterialAsync(int materialId, int count)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.RemoveMaterial, MaterialId = materialId, Count = count });
        }

        /// <summary>
        /// 제작한 포션을 팀 포션 인벤토리에 넣습니다. 인스턴스 ID 는 마스터가 부여하며, Outcome.Potion 에 ID 가 부여된 포션이 담깁니다.
        /// </summary>
        public UniTask<InventoryResult> AddPotionAsync(PotionInstance crafted)
        {
            if (crafted == null || crafted.Data == null)
            {
                return UniTask.FromResult(InventoryResult.Fail(InventoryDenyReason.Invalid));
            }

            return SendRequestAsync(new InventoryCommand
            {
                Op = InventoryOp.AddPotion,
                PotionId = crafted.Data.potionID,
                KeywordId = crafted.TrapKeyword != null ? crafted.TrapKeyword.KeywordID : 0,
            });
        }

        /// <summary>
        /// 팀 포션 인벤토리에서 포션을 꺼냅니다. 여러 명이 같은 포션을 동시에 요청하면 먼저 도착한 한 명만 성공합니다.
        /// </summary>
        public UniTask<InventoryResult> TakePotionAsync(int instanceId)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.TakePotion, InstanceId = instanceId });
        }

        /// <summary>
        /// 죽은 자리에 시체 상자를 만듭니다. slots 는 개인 인벤토리의 TakeAll 결과(칸 위치 그대로)를 넣습니다.
        /// 실패하면 아이템이 사라지지 않도록 호출한 쪽이 slots 를 가지고 있다가 다시 시도하거나 되돌려야 합니다.
        /// </summary>
        public UniTask<InventoryResult> CreateCorpseAsync(Vector3 position, InventorySlot[] slots)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.CreateCorpse, Position = position, Slots = slots });
        }

        /// <summary>
        /// 시체 상자의 칸 하나를 통째로 꺼냅니다. 성공하면 Outcome.Slot 에 꺼낸 내용이 담기며, 받은 쪽이 자기 개인 인벤토리에 넣어야 합니다.
        /// </summary>
        public UniTask<InventoryResult> TakeCorpseSlotAsync(int corpseId, int slotIndex)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.TakeCorpseSlot, CorpseId = corpseId, SlotIndex = slotIndex });
        }

        /// <summary>
        /// 시체 상자의 칸에 내용을 넣습니다. 같은 재료는 한도까지만 합쳐지고, 놓지 못한 만큼은 Outcome.Leftover 로 돌아오므로
        /// 요청한 쪽이 자기 개인 인벤토리에 되돌려야 합니다. 거부되면 아무것도 놓이지 않아 전부 되돌려야 합니다.
        /// </summary>
        public UniTask<InventoryResult> PutCorpseSlotAsync(int corpseId, int slotIndex, InventorySlot content)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.PutCorpseSlot, CorpseId = corpseId, SlotIndex = slotIndex, Slot = content });
        }

        private async UniTask<InventoryResult> SendRequestAsync(InventoryCommand request)
        {
            if (!PhotonNetwork.InRoom)
            {
                return InventoryResult.Fail(InventoryDenyReason.NotInRoom);
            }

            request.RequestId = _nextRequestId++;

            var source = new UniTaskCompletionSource<InventoryResult>();
            _pending[request.RequestId] = source;

            // 내가 마스터이면 이 호출 안에서 바로 처리되어 결과가 이미 채워져 있을 수 있습니다.
            photonView.RPC(nameof(RpcRequest), RpcTarget.MasterClient, InventoryMessages.PackRequest(request));

            (bool isDone, InventoryResult result) = await UniTask.WhenAny(
                source.Task,
                UniTask.Delay(TimeSpan.FromSeconds(_requestTimeoutSeconds), ignoreTimeScale: true));

            if (isDone)
            {
                return result;
            }

            // 제한 시간이 지나도 답이 없으면 포기합니다. 늦게 도착한 적용은 사본에는 반영되지만 이 요청에는 전달되지 않습니다.
            _pending.Remove(request.RequestId);
            Debug.LogWarning($"[InventoryNetworkSyncManager]: {request.Op} 요청({request.RequestId})의 응답이 {_requestTimeoutSeconds}초 안에 오지 않았습니다.");
            return InventoryResult.Fail(InventoryDenyReason.Timeout);
        }

        private void Complete(int requestId, InventoryResult result)
        {
            if (_pending.TryGetValue(requestId, out UniTaskCompletionSource<InventoryResult> source))
            {
                _pending.Remove(requestId);
                source.TrySetResult(result);
            }
        }

        // ==================== 네트워크 메시지 (RPC) ============================

        /// <summary>
        /// [마스터] 요청을 받아 검증합니다. 통과하면 전원에게 적용을 보내고, 거부하면 요청한 사람에게만 이유를 보냅니다.
        /// </summary>
        [PunRPC]
        public void RpcRequest(byte[] data, PhotonMessageInfo info)
        {
            if (!PhotonNetwork.IsMasterClient || info.Sender == null)
            {
                return;
            }

            if (!InventoryMessages.TryUnpackRequest(data, out InventoryCommand request))
            {
                // 깨진 요청이어도 요청 번호를 읽을 수 있으면 바로 거부해서, 요청한 쪽이 제한 시간까지 기다리지 않게 합니다.
                if (InventoryMessages.TryPeekRequestId(data, out int brokenRequestId))
                {
                    photonView.RPC(nameof(RpcDenied), info.Sender, InventoryMessages.PackDeny(brokenRequestId, InventoryDenyReason.Invalid));
                }

                Debug.LogWarning("[InventoryNetworkSyncManager]: 깨진 인벤토리 요청을 받아 무시했습니다.");
                return;
            }

            if (!_state.TryValidate(request, info.Sender.ActorNumber, out InventoryCommand apply, out InventoryDenyReason reason))
            {
                photonView.RPC(nameof(RpcDenied), info.Sender, InventoryMessages.PackDeny(request.RequestId, reason));
                return;
            }

            photonView.RPC(nameof(RpcApply), RpcTarget.All, InventoryMessages.PackApply(apply));
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

            if (!InventoryMessages.TryUnpackApply(data, out InventoryCommand apply))
            {
                // 풀지 못한 적용을 그냥 넘기면 사본이 어긋나므로 전체 목록을 다시 받습니다.
                Debug.LogWarning("[InventoryNetworkSyncManager]: 깨진 적용 메시지를 받아 전체 목록을 다시 요청합니다.");
                RequestResync();
                return;
            }

            InventoryApplyStatus status = _state.Apply(apply, out InventoryOutcome outcome);

            switch (status)
            {
                case InventoryApplyStatus.Applied:
                    if (PhotonNetwork.LocalPlayer != null && apply.RequesterActor == PhotonNetwork.LocalPlayer.ActorNumber)
                    {
                        Complete(apply.RequestId, InventoryResult.Ok(outcome));
                    }
                    break;

                case InventoryApplyStatus.VersionGap:
                case InventoryApplyStatus.Inconsistent:
                    RequestResync();
                    break;
            }
        }

        /// <summary>
        /// [요청한 사람] 마스터가 요청을 거부했음을 받아 기다리던 요청을 실패로 끝냅니다.
        /// </summary>
        [PunRPC]
        public void RpcDenied(byte[] data, PhotonMessageInfo info)
        {
            if (!IsFromMaster(info))
            {
                return;
            }

            if (InventoryMessages.TryUnpackDeny(data, out int requestId, out InventoryDenyReason reason))
            {
                Complete(requestId, InventoryResult.Fail(reason));
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
                Debug.LogWarning("[InventoryNetworkSyncManager]: 전체 목록을 풀지 못했습니다. 기존 사본을 유지합니다.");
            }
        }

        // ==================== 내부 ============================

        private static bool IsFromMaster(PhotonMessageInfo info)
        {
            return info.Sender != null && info.Sender.IsMasterClient;
        }

        private void SendReady()
        {
            photonView.RPC(nameof(RpcReady), RpcTarget.MasterClient);
        }

        // 마스터에게 전체 목록을 다시 요청합니다. 마스터 자신은 어긋날 수 없으므로 보내지 않습니다.
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
