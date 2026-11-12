using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 팀이 함께 쓰는 모든 보관 장소(공용 인벤, 개인 인벤, 공용 창고, 시체 상자, 바닥 주머니)를 네트워크로 동기화하는 매니저입니다.
    /// 씬에 한 개 배치합니다. (PhotonView 필요)
    /// 실제 규칙(검증, 적용, 전체 목록)은 네트워크를 모르는 TeamInventoryState 가 맡고, 이 클래스는 메시지를 주고받는 일만 합니다.
    /// 요청(*Async) → 마스터 검증 → 전원 적용의 흐름과 전체 목록 동기화는 TeamInventoryState 의 주석을 참고하세요.
    /// 오프라인 모드에서는 내가 마스터이므로 같은 코드가 그대로 동작합니다.
    ///
    /// 결과를 알 수 없는 경우(Timeout, OutOfSync)에는 State 를 읽어서 확인하세요. 모든 변경이 사본에 복제되어 있습니다.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public sealed class InventoryNetworkSyncManager : MonoBehaviourPunCallbacks
    {
        public static InventoryNetworkSyncManager Instance { get; private set; }

        [SerializeField, LabelText("요청 제한 시간 (초)"), MinValue(0.5f)]
        private float _requestTimeoutSeconds = 5f;

        // 플레이어가 방을 나갈 때 시체 상자를 둘 위치를 정하는 곳입니다. (예: 마을 중앙) 마지막 위치를 알려 주는 함수가 있으면 그것이 우선입니다.
        [SerializeField, LabelText("나간 플레이어의 시체 상자 위치 (대체)")]
        private Transform _leaveFallbackSpot;

        // 전체 목록을 다시 요청하는 최소 간격입니다. (어긋난 상태에서 메시지가 연달아 와도 요청을 쏟아내지 않기 위함)
        private const float ResyncCooldownSeconds = 1f;

        private TeamInventoryState _state;
        private readonly Dictionary<int, UniTaskCompletionSource<InventoryResult>> _pending = new Dictionary<int, UniTaskCompletionSource<InventoryResult>>();
        private int _nextRequestId = 1;
        private float _lastResyncRequestTime = float.NegativeInfinity;

        /// <summary>
        /// 팀 보관 장소들의 사본입니다. 읽기와 이벤트 구독에 사용하고, 바꾸는 것은 *Async 요청으로만 합니다.
        /// </summary>
        public TeamInventoryState State => _state;

        /// <summary>
        /// 플레이어가 방을 나갔을 때 그 플레이어(ActorNumber)의 마지막 위치를 알려 주는 함수입니다. 지정하지 않으면 대체 위치를 씁니다.
        /// </summary>
        public Func<int, Vector3> PlayerPositionResolver { get; set; }

        /// <summary>
        /// 내 플레이어 번호(ActorNumber)입니다. 방에 없으면 0 입니다.
        /// </summary>
        public int LocalActor => PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;

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
            // 중복으로 만들어져 제거될 매니저는 아무것도 하지 않습니다.
            if (Instance != this)
            {
                return;
            }

            // 이미 방에 있는 상태로 씬이 로딩되었으면 바로 "준비됐다"고 알립니다.
            if (PhotonNetwork.InRoom)
            {
                SendReady();
            }
        }

        public override void OnJoinedRoom()
        {
            if (Instance != this)
            {
                return;
            }

            SendReady();
        }

        /// <summary>
        /// [마스터] 플레이어가 방을 나가면 그 플레이어의 개인 인벤토리를 시체 상자로 남기고 지웁니다.
        /// </summary>
        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            if (Instance != this || !PhotonNetwork.IsMasterClient || otherPlayer == null)
            {
                return;
            }

            Vector3 position = PlayerPositionResolver != null
                ? PlayerPositionResolver(otherPlayer.ActorNumber)
                : (_leaveFallbackSpot != null ? _leaveFallbackSpot.position : Vector3.zero);

            if (_state.TryCreateLeave(otherPlayer.ActorNumber, position, out InventoryCommand apply))
            {
                BroadcastApply(apply);
            }
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
        /// 아이템을 한 장소에서 다른 장소로 옮깁니다. 공용 인벤, 개인 인벤, 공용 창고, 시체 상자 사이의 모든 이동과 바닥 주머니 줍기가 이 요청입니다.
        /// - 칸 구조(개인, 시체 상자)에서 꺼낼 때는 from.Slot 으로 가리키고, 공용 인벤/창고에서 꺼낼 때는 materialId + count 또는 instanceId 로 가리킵니다.
        /// - 받는 곳이 칸 구조이고 to.Slot 이 -1 이면 알아서 고릅니다. (같은 재료 칸을 먼저 채우고, 남으면 빈 칸)
        /// - 가득 차서 일부만 들어가면 들어간 만큼만 옮기고 나머지는 원래 자리에 남습니다. Outcome.Value 가 실제로 옮긴 개수입니다.
        /// - 공용 창고는 베이스캠프에서만 쓸 수 있고, 남의 개인 인벤토리는 쓸 수 없습니다. (NoAccess)
        /// </summary>
        public UniTask<InventoryResult> TransferAsync(ContainerRef from, ContainerRef to, int materialId = 0, int count = 0, int instanceId = 0)
        {
#if UNITY_EDITOR
            // 바닥 주머니 줍기는 요청에 재료 번호/개수를 담지 않고 주머니 번호만 보냅니다. (실제 내용은 마스터가 주머니 데이터에서 읽음)
            // 그래서 로그는 주머니 데이터에서 직접 읽어 보여 줍니다.
            int logMaterialId = materialId;
            int logCount = count;
            if (from.Kind == ContainerKind.Ground && _state != null && _state.GroundDrops.TryGetValue(from.Id, out GroundDrop logDrop))
            {
                logMaterialId = logDrop.MaterialId;
                logCount = count > 0 ? Math.Min(count, logDrop.Count) : logDrop.Count;
            }

            string objName = "재료나 포션이 아닌 아이템입니다";
            if (PotionDataManager.Instance != null)
            {
                if (PotionDataManager.Instance.TryGetMaterialData(logMaterialId, out var materialData))
                {
                    objName = materialData.materialName;
                }
                else if (PotionDataManager.Instance.TryGetPotionData(logMaterialId, out var potionData))
                {
                    objName = potionData.potionName;
                }
            }
            Debug.Log($"[InventoryNetworkSyncManager] : '{objName}'(번호 {logMaterialId}) 아이템을 '{from.Kind}' 에서 '{to.Kind}' 로 옮깁니다. 갯수 : {logCount}개");
#endif
            return SendRequestAsync(new InventoryCommand
            {
                Op = InventoryOp.Transfer,
                From = from,
                To = to,
                MaterialId = materialId,
                Count = count,
                InstanceId = instanceId,
            });
        }

        /// <summary>
        /// 바닥 재료 주머니를 통째로 줍습니다. to 를 지정하지 않으면 공용 인벤으로 받습니다.
        /// 공용 인벤이 가득 차서 일부만 들어가면 남은 만큼은 주머니에 남습니다. 두 명이 동시에 주우면 먼저 도착한 한 명만 성공합니다.
        /// </summary>
        public UniTask<InventoryResult> PickupAsync(int dropId, ContainerRef to = default)
        {
#if UNITY_EDITOR
            Debug.Log($"[InventoryNetworkSyncManager] : 플레이어가 재료를 줍습니다");
#endif

            if (to.Kind == ContainerKind.None)
            {
                to = ContainerRef.PackRef;
            }

            return TransferAsync(ContainerRef.Ground(dropId), to);
        }

        /// <summary>
        /// 내 개인 인벤토리 안에서 칸을 옮깁니다. (빈 칸이면 이동, 같은 재료면 합침, 그 외에는 서로 바꿈)
        /// 다른 사람과 충돌하지 않으므로 마스터가 확정하기 전에 내 화면에는 먼저 반영하고, 마스터가 거부하면 전체 목록으로 바로잡습니다.
        /// </summary>
        public async UniTask<InventoryResult> MovePersonalAsync(int fromSlot, int toSlot)
        {
            int actor = LocalActor;
            if (actor <= 0)
            {
                return InventoryResult.Fail(InventoryDenyReason.NotInRoom);
            }

            // 마스터는 곧바로 같은 결과가 적용되므로 따로 미리 반영하지 않습니다.
            bool predicted = false;
            PersonalInventory mine = _state.GetPersonal(actor);
            if (!PhotonNetwork.IsMasterClient && mine != null)
            {
                predicted = mine.Move(fromSlot, toSlot);
            }

            InventoryResult result = await TransferAsync(ContainerRef.Personal(actor, fromSlot), ContainerRef.Personal(actor, toSlot));

            if (!result.Success && predicted)
            {
                RequestResync();
            }

            return result;
        }

        /// <summary>
        /// 재료 두 개(재료+재료 또는 같은 포션+같은 포션)를 조합합니다. 재료는 공용 인벤 또는 내 개인 인벤에서만 쓸 수 있고, 창고는 쓸 수 없습니다.
        /// 마스터가 레시피, 재료 보유, 결과를 놓을 자리를 한 번에 확인하고 재료 차감과 포션 추가를 함께 적용합니다. 어느 하나라도 안 되면 아무것도 바뀌지 않습니다.
        /// 결과 포션은 내 개인 인벤토리에 받고, 빈 칸이 없으면 공용 인벤에 받습니다. Outcome.Potion 이 ID 가 부여된 포션입니다.
        /// keywordId 는 제작 실패 판정(PotionCrafter)으로 정해진 함정 키워드이고, 정상이면 0 입니다.
        /// </summary>
        public UniTask<InventoryResult> CraftAsync(Ingredient first, Ingredient second, int keywordId = 0)
        {
            return SendRequestAsync(new InventoryCommand
            {
                Op = InventoryOp.Craft,
                IngredientA = first,
                IngredientB = second,
                KeywordId = keywordId,
            });
        }

        /// <summary>
        /// 포션을 무작위로 꺼내 손에 들 준비를 합니다. (F 키 꺼내기)
        /// 공용 인벤에서 count 개(1~2)를 무작위로 꺼내고, 공용 인벤에 포션이 모자라면 모자란 만큼 내 개인 인벤토리에서 꺼냅니다.
        /// 꺼낸 포션은 인벤토리에서 사라지고 Outcome.Potions 로 돌아오니, 받은 포션으로 손에 들 오브젝트를 만드세요.
        /// 포션이 하나도 없으면 NotFound 로 실패하니 그냥 건너뛰면 됩니다. count 는 비어 있는 손의 수로 부르세요.
        /// 일부만 꺼낼 수 있으면 꺼낸 만큼만 성공합니다. (Outcome.Potions.Count 확인)
        /// </summary>
        public UniTask<InventoryResult> TakeOutRandomAsync(int count)
        {
            if (count < 1 || count > TeamInventoryState.MaxTakeOutCount)
            {
                return UniTask.FromResult(InventoryResult.Fail(InventoryDenyReason.Invalid));
            }

            return SendRequestAsync(new InventoryCommand
            {
                Op = InventoryOp.TakeOut,
                Count = count,
                From = new ContainerRef { Kind = ContainerKind.None, Slot = -1 },
            });
        }

        /// <summary>
        /// 개인 인벤토리의 퀵슬롯(칸 번호 0~9, 키보드 1~0)에 든 포션을 정확히 그 칸에서 꺼냅니다.
        /// 칸이 비었으면 SlotEmpty, 재료이면 Invalid 로 실패합니다. 성공하면 Outcome.Potions 에 한 개가 들어 있습니다.
        /// </summary>
        public UniTask<InventoryResult> TakeOutQuickSlotAsync(int slot)
        {
            // 음수는 "무작위"로 해석되고 254 를 넘으면 메시지에 담을 수 없으므로 미리 거부합니다.
            if (slot < 0 || slot > 254)
            {
                return UniTask.FromResult(InventoryResult.Fail(InventoryDenyReason.Invalid));
            }

            return SendRequestAsync(new InventoryCommand
            {
                Op = InventoryOp.TakeOut,
                Count = 1,
                From = new ContainerRef { Kind = ContainerKind.None, Slot = slot },
            });
        }

        /// <summary>
        /// 내 개인 인벤토리의 첫 번째 아이템(앞 칸부터 처음 나오는 것) 한 개를 소모합니다. (허기)
        /// 재료는 한 개, 포션은 그 포션을 없앱니다. 개인 인벤토리가 비어 있으면 NotFound 로 실패하니 아무 일도 일어나지 않은 것으로 보면 됩니다.
        /// </summary>
        public UniTask<InventoryResult> ConsumeFirstPersonalAsync()
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.Consume });
        }

        /// <summary>
        /// 죽었을 때 호출합니다. 마스터가 내 개인 인벤토리의 내용을 시체 상자로 옮깁니다. 남길 것이 없으면 Invalid 로 거부됩니다.
        /// 공용 인벤과 공용 창고는 그대로 남습니다. Outcome.CorpseId 가 만들어진 상자의 ID 입니다.
        /// </summary>
        public UniTask<InventoryResult> DieAsync(Vector3 position)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.Die, Position = position });
        }

        /// <summary>
        /// [개발용] 재료를 만들어 넣습니다. TeamInventoryState.AllowGrant 가 꺼져 있으면 거부됩니다.
        /// </summary>
        public UniTask<InventoryResult> GrantMaterialAsync(ContainerRef to, int materialId, int count)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.Grant, To = to, MaterialId = materialId, Count = count });
        }

        /// <summary>
        /// [개발용] 포션을 만들어 넣습니다. 인스턴스 ID 는 마스터가 부여합니다. TeamInventoryState.AllowGrant 가 꺼져 있으면 거부됩니다.
        /// </summary>
        public UniTask<InventoryResult> GrantPotionAsync(ContainerRef to, int potionId, int keywordId = 0)
        {
            return SendRequestAsync(new InventoryCommand { Op = InventoryOp.Grant, To = to, PotionId = potionId, KeywordId = keywordId });
        }

        // ==================== 마스터 전용 ============================

        /// <summary>
        /// [마스터 전용] 바닥에 재료 주머니를 만들어 모든 클라이언트에 알립니다. 월드 오브젝트가 소진될 때 마스터가 호출합니다.
        /// 마스터가 아니거나 만들 것이 없으면 false 입니다.
        /// </summary>
        public bool SpawnGroundDrops(IReadOnlyList<GroundSpawn> spawns)
        {
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            {
                return false;
            }

            if (!_state.TryCreateGroundSpawn(spawns, out InventoryCommand apply))
            {
                return false;
            }

            BroadcastApply(apply);
            return true;
        }

        /// <summary>
        /// [마스터 전용] 상호작용으로 얻은 월드 오브젝트의 재료를 공용 인벤에 바로 넣습니다. 한도를 넘는 만큼은 각 spawn 의 위치에 바닥 주머니로 남깁니다.
        /// 마스터가 아니거나 넣을 것이 없으면 false 입니다.
        /// </summary>
        public bool GrantWorldDrops(IReadOnlyList<GroundSpawn> grants)
        {
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            {
                return false;
            }

            if (!_state.TryCreateWorldGrant(grants, out InventoryCommand apply))
            {
                return false;
            }

            BroadcastApply(apply);
            return true;
        }

        private void BroadcastApply(InventoryCommand apply)
        {
            photonView.RPC(nameof(RpcApply), RpcTarget.All, InventoryMessages.PackApply(apply));
        }

        // ==================== 요청 처리 ============================

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
            Debug.LogWarning($"[InventoryNetworkSyncManager]: {request.Op} 요청({request.RequestId})의 응답이 {_requestTimeoutSeconds}초 안에 오지 않았습니다. 결과는 State 로 확인하세요.");
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

            BroadcastApply(apply);
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
                    if (IsLocalActor(apply.RequesterActor))
                    {
                        Complete(apply.RequestId, InventoryResult.Ok(outcome));
                    }
                    break;

                case InventoryApplyStatus.Ignored:
                case InventoryApplyStatus.VersionGap:
                case InventoryApplyStatus.Inconsistent:
                    // 내 요청의 적용인데 사본에 반영하지 못했다면 결과를 알 수 없습니다. 제한 시간까지 기다리지 않고 바로 알립니다.
                    if (IsLocalActor(apply.RequesterActor))
                    {
                        Complete(apply.RequestId, InventoryResult.Fail(InventoryDenyReason.OutOfSync));
                    }

                    // Ignored 는 이미 반영된 오래된 메시지라 전체 목록을 다시 받을 필요는 없습니다.
                    if (status != InventoryApplyStatus.Ignored)
                    {
                        RequestResync();
                    }
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
        /// 요청한 클라이언트는 이 목록을 받을 때까지 기다리던 마스터 밖의 변경이 없으므로 안전합니다.
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

        private static bool IsLocalActor(int actorNumber)
        {
            return PhotonNetwork.LocalPlayer != null && actorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
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
