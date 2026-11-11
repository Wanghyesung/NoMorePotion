using System;
using System.Collections.Generic;
using System.IO;
using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 마스터가 바닥에 만들 재료 주머니 한 개의 정보입니다. (재료, 개수, 위치)
    /// </summary>
    public struct GroundSpawn
    {
        public int MaterialId;
        public int Count;
        public Vector3 Position;
    }

    /// <summary>
    /// 팀이 함께 쓰는 모든 보관 장소의 사본입니다. 모든 클라이언트가 같은 사본을 하나씩 가집니다.
    /// 네트워크 라이브러리를 모르는 순수 C# 클래스라서, 마스터 한 개와 클라이언트 여러 개를 흉내 내어 에디터에서 시험할 수 있습니다.
    ///
    /// 보관 장소 : 공용 인벤(Pack, 어디서든) / 플레이어별 개인 인벤(Personal, 본인만) / 공용 창고(Storage, 베이스캠프에서만) /
    ///            시체 상자(Corpse) / 바닥 재료 주머니(Ground)
    ///
    /// 흐름
    /// 1) 클라이언트가 요청(InventoryCommand)을 마스터에게 보냅니다.
    /// 2) 마스터가 <see cref="TryValidate"/> 로 검증합니다. 사본 내용은 바꾸지 않고, "어떻게 바뀌는가"를 변경 목록(Changes)으로 만들어 돌려줍니다.
    ///    거부되면 이유를 요청한 사람에게만 알립니다.
    /// 3) 변경 목록을 마스터를 포함한 모든 클라이언트가 <see cref="Apply"/> 로 똑같이 적용합니다. (변경은 모두 "절대값"이라 같은 결과가 나옵니다)
    /// 두 장소에 걸친 작업(이동, 조합, 줍기, 사망)도 마스터가 한 번에 검증하고 한 번에 적용하므로 중간에 끊겨 아이템이 사라지지 않습니다.
    ///
    /// 모든 적용에는 사본 버전이 붙고, 번호가 이어지지 않으면(중간 메시지를 놓침) VersionGap 을 알려 전체 목록(스냅샷)을 다시 받게 합니다.
    /// 사본 내용은 바꾸지 않지만, 발급 번호(포션 인스턴스 ID, 시체 상자 ID, 바닥 주머니 ID)는 검증할 때 소비합니다.
    /// (적용 메시지가 도착하기 전에 다음 요청이 검증되어도 같은 번호가 나가지 않게 하려는 것입니다)
    /// </summary>
    public sealed class TeamInventoryState
    {
        /// <summary>
        /// 시체 상자의 칸 수는 byte 1개로 보내므로 255칸이 한계입니다.
        /// </summary>
        public const int MaxCorpseSlots = 255;

        private readonly IInventoryData _data;
        private readonly Dictionary<int, PersonalInventory> _personals = new Dictionary<int, PersonalInventory>();
        private readonly Dictionary<int, CorpseBox> _corpses = new Dictionary<int, CorpseBox>();
        private readonly Dictionary<int, GroundDrop> _ground = new Dictionary<int, GroundDrop>();

        // 발급 번호입니다. 마스터만 발급하며, 스냅샷에 담아 두어 새로 들어온 사람도 이어 쓸 수 있게 합니다.
        private int _nextCorpseId = 1;
        private int _nextDropId = 1;

        private Func<int, bool> _storageAccessGate;

        // 한 번의 적용 안에서 바뀐 것을 모았다가, 모두 적용한 뒤에 이벤트를 발생시킵니다. (반쯤 적용된 상태가 화면에 보이지 않게)
        private readonly HashSet<int> _createdCorpses = new HashSet<int>();
        private readonly HashSet<int> _changedCorpses = new HashSet<int>();
        private readonly HashSet<int> _createdDrops = new HashSet<int>();
        private readonly HashSet<int> _changedDrops = new HashSet<int>();

        /// <param name="data">재료 한도, 포션 조회, 레시피를 알려 주는 창구. 지정하지 않으면 씬의 PotionDataManager 를 사용</param>
        /// <param name="storageAccessGate">플레이어(ActorNumber)가 공용 창고를 쓸 수 있는지 알려 주는 함수. 지정하지 않으면 항상 허용</param>
        public TeamInventoryState(IInventoryData data = null, Func<int, bool> storageAccessGate = null)
        {
            _data = data ?? DefaultInventoryData.Instance;
            _storageAccessGate = storageAccessGate;

            Pack = new ItemStore(new MaterialInventory(_data.GetMaterialLimit), new PotionInventory());
            Storage = new ItemStore(
                new MaterialInventory(id => _data.GetMaterialLimit(id) * InventoryRules.StorageMaterialMultiplier),
                new PotionInventory(InventoryRules.StoragePotionCapacity));
        }

        /// <summary>
        /// 공용 인벤(배낭)입니다. 맵 어디서든 쓸 수 있습니다.
        /// </summary>
        public ItemStore Pack { get; }

        /// <summary>
        /// 공용 창고입니다. 베이스캠프에서만 쓸 수 있습니다. (판정 함수는 SetStorageAccessGate 로 지정)
        /// </summary>
        public ItemStore Storage { get; }

        /// <summary>
        /// 공용 인벤의 재료입니다. (Pack.Materials 의 줄임)
        /// </summary>
        public MaterialInventory Materials => Pack.Materials;

        /// <summary>
        /// 공용 인벤의 포션입니다. (Pack.Potions 의 줄임)
        /// </summary>
        public PotionInventory Potions => Pack.Potions;

        /// <summary>
        /// 플레이어별 개인 인벤토리입니다. 키는 ActorNumber 입니다. 모든 클라이언트가 가지지만 화면에는 내 것만 보여 줍니다. (읽기 전용)
        /// </summary>
        public IReadOnlyDictionary<int, PersonalInventory> Personals => _personals;

        /// <summary>
        /// 시체 상자들입니다. (읽기 전용)
        /// </summary>
        public IReadOnlyDictionary<int, CorpseBox> Corpses => _corpses;

        /// <summary>
        /// 바닥에 떨어진 재료 주머니들입니다. 키는 주머니 ID 입니다. (읽기 전용)
        /// </summary>
        public IReadOnlyDictionary<int, GroundDrop> GroundDrops => _ground;

        /// <summary>
        /// 마지막으로 적용한 사본 버전입니다. 적용할 때마다 1씩 오릅니다.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// 개발용 아이템 생성(Grant)을 허용할지 여부입니다. 출시 빌드에서는 끄세요.
        /// </summary>
        public bool AllowGrant { get; set; } = true;

        /// <summary>
        /// 플레이어가 공용 창고를 쓸 수 있는지 알려 주는 함수를 지정합니다. (예: 베이스캠프 영역 안에 있는가) 마스터의 검증에서 호출합니다.
        /// null 이면 항상 허용합니다.
        /// </summary>
        public void SetStorageAccessGate(Func<int, bool> gate)
        {
            _storageAccessGate = gate;
        }

        /// <summary>
        /// 해당 플레이어의 개인 인벤토리입니다. 아직 없으면 null 입니다.
        /// </summary>
        public PersonalInventory GetPersonal(int actor)
        {
            return _personals.TryGetValue(actor, out PersonalInventory inventory) ? inventory : null;
        }

        public event Action<int> OnPersonalAdded;
        public event Action<int> OnPersonalRemoved;

        /// <summary>
        /// 시체 상자가 새로 생겼을 때 그 ID 와 함께 발생합니다. (내용이 채워진 뒤)
        /// </summary>
        public event Action<int> OnCorpseCreated;

        /// <summary>
        /// 시체 상자의 내용이 바뀌었을 때(꺼냄, 넣음) 그 ID 와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnCorpseChanged;

        /// <summary>
        /// 시체 상자가 사라졌을 때(내용이 모두 비어짐) 그 ID 와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnCorpseRemoved;

        /// <summary>
        /// 바닥에 재료 주머니가 생겼을 때 그 ID 와 함께 발생합니다. 화면의 주머니 오브젝트를 만드는 쪽이 구독합니다.
        /// </summary>
        public event Action<int> OnGroundAdded;

        /// <summary>
        /// 바닥 주머니의 남은 개수가 바뀌었을 때(일부만 주움) 그 ID 와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnGroundChanged;

        /// <summary>
        /// 바닥 주머니가 사라졌을 때(전부 주움) 그 ID 와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnGroundRemoved;

        /// <summary>
        /// 스냅샷으로 전체 상태가 통째로 바뀌었을 때 발생합니다. (보여 주는 쪽이 처음부터 다시 그려야 함)
        /// </summary>
        public event Action OnReloaded;

        // ==================== 마스터: 요청 검증 ============================

        /// <summary>
        /// 요청을 검증합니다. (마스터에서만 호출) 사본의 내용은 바꾸지 않습니다. 단 발급 번호는 소비합니다.
        /// 통과하면 모든 클라이언트가 적용할 명령(apply, 변경 목록 포함)을 만들고, 거부하면 이유(reason)를 돌려줍니다.
        /// </summary>
        /// <param name="requesterActor">요청한 사람의 ActorNumber</param>
        public bool TryValidate(InventoryCommand request, int requesterActor, out InventoryCommand apply, out InventoryDenyReason reason)
        {
            apply = request;
            apply.RequesterActor = requesterActor;
            apply.Version = Version + 1;
            apply.Changes = null;
            reason = InventoryDenyReason.None;

            switch (request.Op)
            {
                case InventoryOp.Transfer:
                    return ValidateTransfer(request, requesterActor, ref apply, out reason);

                case InventoryOp.Craft:
                    return ValidateCraft(request, requesterActor, ref apply, out reason);

                case InventoryOp.Die:
                    return BuildDie(requesterActor, request.Position, false, ref apply, out reason);

                case InventoryOp.Grant:
                    return ValidateGrant(request, requesterActor, ref apply, out reason);

                case InventoryOp.TakeOut:
                    return ValidateTakeOut(request, requesterActor, ref apply, out reason);

                default:
                    // SpawnGround, Leave 는 클라이언트가 요청할 수 없는 마스터 전용 변경입니다.
                    reason = InventoryDenyReason.Invalid;
                    return false;
            }
        }

        /// <summary>
        /// [마스터 전용] 플레이어가 방을 나갔을 때의 변경을 만듭니다. 개인 인벤토리의 내용은 시체 상자가 되고, 개인 인벤토리는 사라집니다.
        /// 개인 인벤토리가 없으면 false 입니다.
        /// </summary>
        public bool TryCreateLeave(int actor, Vector3 position, out InventoryCommand apply)
        {
            apply = new InventoryCommand { Op = InventoryOp.Leave, RequesterActor = actor, Version = Version + 1 };

            if (!_personals.ContainsKey(actor))
            {
                return false;
            }

            return BuildDie(actor, position, true, ref apply, out InventoryDenyReason _);
        }

        /// <summary>
        /// [마스터 전용] 바닥에 재료 주머니를 만드는 변경을 만듭니다. 알 수 없는 재료나 0 이하의 개수는 건너뛰고, 만들 것이 하나도 없으면 false 입니다.
        /// </summary>
        public bool TryCreateGroundSpawn(IReadOnlyList<GroundSpawn> spawns, out InventoryCommand apply)
        {
            apply = new InventoryCommand { Op = InventoryOp.SpawnGround, Version = Version + 1, Changes = new List<InventoryChange>() };

            if (spawns == null)
            {
                return false;
            }

            for (int i = 0; i < spawns.Count; i++)
            {
                GroundSpawn spawn = spawns[i];

                if (spawn.Count <= 0 || !_data.IsKnownMaterial(spawn.MaterialId))
                {
                    continue;
                }

                apply.Changes.Add(InventoryChange.CreateGround(_nextDropId++, spawn.MaterialId, spawn.Count, spawn.Position));
            }

            return apply.Changes.Count > 0;
        }

        /// <summary>
        /// [마스터 전용] 월드 오브젝트의 재료를 공용 인벤에 바로 넣는 변경을 만듭니다. (상호작용으로 얻은 재료)
        /// 공용 인벤의 한도까지 넣고, 넘치는 만큼은 각 spawn 의 위치에 바닥 주머니로 만듭니다.
        /// 알 수 없는 재료나 0 이하의 개수는 건너뛰고, 바뀔 것이 없으면 false 입니다. 클라이언트가 요청할 수 없는 변경입니다.
        /// </summary>
        public bool TryCreateWorldGrant(IReadOnlyList<GroundSpawn> grants, out InventoryCommand apply)
        {
            apply = new InventoryCommand { Op = InventoryOp.SpawnGround, Version = Version + 1, Changes = new List<InventoryChange>() };

            if (grants == null)
            {
                return false;
            }

            // 같은 재료가 여러 건이어도 합쳐서 계산합니다. (한 변경 목록에 같은 재료의 SetMaterial 이 중복되지 않게)
            var added = new Dictionary<int, int>();
            var overflow = new List<GroundSpawn>();

            for (int i = 0; i < grants.Count; i++)
            {
                GroundSpawn grant = grants[i];

                if (grant.Count <= 0 || !_data.IsKnownMaterial(grant.MaterialId))
                {
                    continue;
                }

                added.TryGetValue(grant.MaterialId, out int already);
                int room = Math.Max(0, Pack.Materials.GetRoom(grant.MaterialId) - already);
                int accepted = Math.Min(grant.Count, room);

                if (accepted > 0)
                {
                    added[grant.MaterialId] = already + accepted;
                }

                if (grant.Count - accepted > 0)
                {
                    overflow.Add(new GroundSpawn { MaterialId = grant.MaterialId, Count = grant.Count - accepted, Position = grant.Position });
                }
            }

            foreach (KeyValuePair<int, int> entry in added)
            {
                apply.Changes.Add(InventoryChange.SetMaterial(ContainerRef.PackRef, entry.Key, Pack.Materials.GetCount(entry.Key) + entry.Value));
            }

            for (int i = 0; i < overflow.Count; i++)
            {
                apply.Changes.Add(InventoryChange.CreateGround(_nextDropId++, overflow[i].MaterialId, overflow[i].Count, overflow[i].Position));
            }

            return apply.Changes.Count > 0;
        }

        // ---------- 이동 ----------

        private bool ValidateTransfer(InventoryCommand request, int actor, ref InventoryCommand apply, out InventoryDenyReason reason)
        {
            ContainerRef from = request.From;
            ContainerRef to = request.To;

            if (!CheckAccess(from, actor, true, out reason) || !CheckAccess(to, actor, false, out reason))
            {
                return false;
            }

            var ctx = new Context(this);

            // 같은 칸 구조 안에서의 이동 (개인 칸끼리, 한 시체 상자 안에서): 빈 칸이면 이동, 같은 재료면 합침, 그 외에는 서로 바꿈
            if (from.IsSlotBased && to.IsSlotBased && from.IsSameContainer(to))
            {
                PersonalInventory inventory = ctx.Slots(from);

                if (from.Slot < 0 || from.Slot >= inventory.Capacity || inventory.Slots[from.Slot].IsEmpty)
                {
                    reason = InventoryDenyReason.SlotEmpty;
                    return false;
                }

                if (to.Slot < 0 || to.Slot >= inventory.Capacity)
                {
                    reason = InventoryDenyReason.Invalid;
                    return false;
                }

                if (!inventory.Move(from.Slot, to.Slot))
                {
                    reason = InventoryDenyReason.Blocked;
                    return false;
                }

                apply.Count = 1;
                apply.Changes = ctx.Flush();
                return true;
            }

            // 같은 공용 인벤끼리, 같은 창고끼리 등 의미 없는 이동은 거부합니다.
            if (from.IsSameContainer(to))
            {
                reason = InventoryDenyReason.Invalid;
                return false;
            }

            // 1) 옮길 내용을 정합니다.
            InventorySlot item;
            GroundDrop drop = null;

            switch (from.Kind)
            {
                case ContainerKind.Personal:
                case ContainerKind.Corpse:
                {
                    PersonalInventory source = ctx.Slots(from);

                    if (from.Slot < 0 || from.Slot >= source.Capacity || source.Slots[from.Slot].IsEmpty)
                    {
                        reason = InventoryDenyReason.SlotEmpty;
                        return false;
                    }

                    item = source.Slots[from.Slot];

                    // 재료는 개수를 지정하면 그만큼만 옮깁니다. (0 이면 칸 전체)
                    if (item.Kind == InventorySlotKind.Material && request.Count > 0)
                    {
                        item.Count = Math.Min(request.Count, item.Count);
                    }
                    break;
                }

                case ContainerKind.Pack:
                case ContainerKind.Storage:
                {
                    ItemStore store = GetStore(from.Kind);

                    if (request.InstanceId > 0)
                    {
                        int index = store.Potions.IndexOf(request.InstanceId);
                        if (index < 0 || ctx.IsPotionRemoved(from.Kind, request.InstanceId))
                        {
                            reason = InventoryDenyReason.NotFound;
                            return false;
                        }

                        item = InventorySlot.OfPotion(store.Potions.Items[index]);
                    }
                    else
                    {
                        if (request.MaterialId <= 0 || request.Count <= 0)
                        {
                            reason = InventoryDenyReason.Invalid;
                            return false;
                        }

                        if (store.Materials.GetCount(request.MaterialId) < request.Count)
                        {
                            reason = InventoryDenyReason.NotEnough;
                            return false;
                        }

                        item = InventorySlot.OfMaterial(request.MaterialId, request.Count);
                    }
                    break;
                }

                case ContainerKind.Ground:
                    drop = _ground[from.Id];
                    item = InventorySlot.OfMaterial(drop.MaterialId, request.Count > 0 ? Math.Min(request.Count, drop.Count) : drop.Count);
                    break;

                default:
                    reason = InventoryDenyReason.Invalid;
                    return false;
            }

            // 2) 받는 곳에 넣어 봅니다. 들어간 만큼(moved)만 옮깁니다. (가득 차서 일부만 들어가면 나머지는 원래 자리에 남음)
            if (!TryPlace(ctx, to, item, out int moved, out reason))
            {
                return false;
            }

            // 3) 원래 자리에서 옮긴 만큼 뺍니다.
            switch (from.Kind)
            {
                case ContainerKind.Personal:
                case ContainerKind.Corpse:
                {
                    PersonalInventory source = ctx.Slots(from);

                    if (item.Kind == InventorySlotKind.Potion || moved >= source.Slots[from.Slot].Count)
                    {
                        source.TakeSlot(from.Slot);
                    }
                    else
                    {
                        source.TakeFromSlot(from.Slot, moved);
                    }
                    break;
                }

                case ContainerKind.Pack:
                case ContainerKind.Storage:
                    if (item.Kind == InventorySlotKind.Potion)
                    {
                        ctx.RemovePotion(from.Kind, item.Potion.InstanceId);
                    }
                    else
                    {
                        ctx.AddMaterialDelta(from.Kind, item.MaterialId, -moved);
                    }
                    break;

                case ContainerKind.Ground:
                    ctx.Changes.Add(InventoryChange.SetGround(from.Id, drop.Count - moved));
                    break;
            }

            apply.Count = moved;
            apply.Changes = ctx.Flush();
            return true;
        }

        /// <summary>
        /// 받는 곳에 아이템을 넣어 보고, 실제로 들어간 개수를 돌려줍니다. 사본은 바꾸지 않고 검증 맥락(ctx)에만 기록합니다.
        /// </summary>
        private bool TryPlace(Context ctx, ContainerRef to, InventorySlot item, out int moved, out InventoryDenyReason reason)
        {
            moved = 0;
            reason = InventoryDenyReason.None;

            switch (to.Kind)
            {
                case ContainerKind.Personal:
                case ContainerKind.Corpse:
                {
                    PersonalInventory destination = ctx.Slots(to);

                    if (item.Kind == InventorySlotKind.Material)
                    {
                        if (to.Slot >= 0)
                        {
                            // 지정한 칸에 놓습니다. 빈 칸이거나 같은 재료일 때만 가능하고, 한도를 넘는 만큼은 못 놓습니다.
                            if (to.Slot >= destination.Capacity)
                            {
                                reason = InventoryDenyReason.Invalid;
                                return false;
                            }

                            if (!destination.TryPlace(to.Slot, item, out InventorySlot leftover))
                            {
                                reason = InventoryDenyReason.Blocked;
                                return false;
                            }

                            moved = item.Count - leftover.Count;
                        }
                        else
                        {
                            // 알아서 고르기: 같은 재료가 든 칸을 먼저 채우고, 남으면 빈 칸을 씁니다.
                            int remaining = destination.AddMaterial(item.MaterialId, item.Count);
                            moved = item.Count - remaining;

                            if (moved <= 0)
                            {
                                reason = InventoryDenyReason.Full;
                                return false;
                            }
                        }
                    }
                    else
                    {
                        if (to.Slot >= 0)
                        {
                            if (to.Slot >= destination.Capacity)
                            {
                                reason = InventoryDenyReason.Invalid;
                                return false;
                            }

                            if (!destination.TryPlace(to.Slot, item, out InventorySlot _))
                            {
                                reason = InventoryDenyReason.Blocked;
                                return false;
                            }
                        }
                        else if (destination.AddPotion(item.Potion) < 0)
                        {
                            reason = InventoryDenyReason.Full;
                            return false;
                        }

                        moved = 1;
                    }

                    return true;
                }

                case ContainerKind.Pack:
                case ContainerKind.Storage:
                {
                    ItemStore store = GetStore(to.Kind);

                    if (item.Kind == InventorySlotKind.Material)
                    {
                        int room = store.Materials.GetRoom(item.MaterialId) - ctx.MaterialDelta(to.Kind, item.MaterialId);
                        moved = Math.Min(item.Count, Math.Max(0, room));

                        if (moved <= 0)
                        {
                            reason = InventoryDenyReason.Full;
                            return false;
                        }

                        ctx.AddMaterialDelta(to.Kind, item.MaterialId, moved);
                    }
                    else
                    {
                        if (store.Potions.IsFull)
                        {
                            reason = InventoryDenyReason.Full;
                            return false;
                        }

                        if (store.Potions.Contains(item.Potion.InstanceId))
                        {
                            reason = InventoryDenyReason.Invalid;
                            return false;
                        }

                        ctx.AddPotion(to.Kind, item.Potion);
                        moved = 1;
                    }

                    return true;
                }

                default:
                    reason = InventoryDenyReason.Invalid;   // 바닥에는 놓을 수 없습니다.
                    return false;
            }
        }

        // ---------- 조합 ----------

        private bool ValidateCraft(InventoryCommand request, int actor, ref InventoryCommand apply, out InventoryDenyReason reason)
        {
            var ctx = new Context(this);

            if (!ConsumeIngredient(ctx, request.IngredientA, actor, out bool isMaterialA, out int dataIdA, out reason)
                || !ConsumeIngredient(ctx, request.IngredientB, actor, out bool isMaterialB, out int dataIdB, out reason))
            {
                return false;
            }

            // 재료끼리 또는 포션끼리만 조합할 수 있습니다.
            if (isMaterialA != isMaterialB)
            {
                reason = InventoryDenyReason.Invalid;
                return false;
            }

            PotionData result = isMaterialA ? _data.FindRecipe(dataIdA, dataIdB) : _data.FindUpgrade(dataIdA, dataIdB);
            if (result == null)
            {
                reason = InventoryDenyReason.Invalid;
                return false;
            }

            TrapPotionKeyword keyword = null;
            if (request.KeywordId != 0 && !_data.TryGetKeyword(request.KeywordId, out keyword))
            {
                reason = InventoryDenyReason.Invalid;
                return false;
            }

            // 결과 포션은 개인 인벤토리에 받고, 빈 칸이 없으면 공용 인벤에 받습니다. 둘 다 안 되면 거부합니다. (재료는 그대로)
            PersonalInventory personal = ctx.Slots(ContainerRef.Personal(actor));
            bool toPersonal = FindEmptySlot(personal) >= 0;

            if (!toPersonal && Pack.Potions.IsFull)
            {
                reason = InventoryDenyReason.Full;
                return false;
            }

            // 모든 검사를 통과한 뒤에 인스턴스 ID 를 발급해서 번호를 낭비하지 않습니다.
            var potion = new PotionInstance(result, keyword, PotionInstanceIds.Next());

            if (toPersonal)
            {
                personal.AddPotion(potion);
            }
            else
            {
                ctx.AddPotion(ContainerKind.Pack, potion);
            }

            apply.InstanceId = potion.InstanceId;
            apply.Count = 1;
            apply.Changes = ctx.Flush();
            return true;
        }

        /// <summary>
        /// 조합 재료 한 개를 확인하고 소비한 것으로 검증 맥락에 기록합니다. 조합은 개인 인벤과 공용 인벤에서만 할 수 있습니다. (창고 불가)
        /// </summary>
        private bool ConsumeIngredient(Context ctx, Ingredient ingredient, int actor, out bool isMaterial, out int dataId, out InventoryDenyReason reason)
        {
            isMaterial = false;
            dataId = 0;
            reason = InventoryDenyReason.None;

            switch (ingredient.Source.Kind)
            {
                case ContainerKind.Pack:
                {
                    if (ingredient.InstanceId > 0)
                    {
                        int index = Pack.Potions.IndexOf(ingredient.InstanceId);
                        if (index < 0 || ctx.IsPotionRemoved(ContainerKind.Pack, ingredient.InstanceId))
                        {
                            reason = InventoryDenyReason.NotFound;
                            return false;
                        }

                        dataId = Pack.Potions.Items[index].Data.potionID;
                        ctx.RemovePotion(ContainerKind.Pack, ingredient.InstanceId);
                        return true;
                    }

                    if (ingredient.MaterialId <= 0 || !_data.IsKnownMaterial(ingredient.MaterialId))
                    {
                        reason = InventoryDenyReason.Invalid;
                        return false;
                    }

                    int have = Pack.Materials.GetCount(ingredient.MaterialId) + ctx.MaterialDelta(ContainerKind.Pack, ingredient.MaterialId);
                    if (have < 1)
                    {
                        reason = InventoryDenyReason.NotEnough;
                        return false;
                    }

                    ctx.AddMaterialDelta(ContainerKind.Pack, ingredient.MaterialId, -1);
                    isMaterial = true;
                    dataId = ingredient.MaterialId;
                    return true;
                }

                case ContainerKind.Personal:
                {
                    if (ingredient.Source.Id != actor)
                    {
                        reason = InventoryDenyReason.NoAccess;
                        return false;
                    }

                    PersonalInventory inventory = ctx.Slots(ingredient.Source);
                    int slotIndex = ingredient.Source.Slot;

                    if (slotIndex < 0 || slotIndex >= inventory.Capacity || inventory.Slots[slotIndex].IsEmpty)
                    {
                        reason = InventoryDenyReason.SlotEmpty;
                        return false;
                    }

                    InventorySlot content = inventory.Slots[slotIndex];

                    if (content.Kind == InventorySlotKind.Material)
                    {
                        if (!inventory.TakeFromSlot(slotIndex, 1))
                        {
                            reason = InventoryDenyReason.NotEnough;
                            return false;
                        }

                        isMaterial = true;
                        dataId = content.MaterialId;
                        return true;
                    }

                    dataId = content.Potion.Data.potionID;
                    inventory.TakeSlot(slotIndex);
                    return true;
                }

                case ContainerKind.Storage:
                    reason = InventoryDenyReason.NoAccess;   // 창고에서는 조합하지 않습니다.
                    return false;

                default:
                    reason = InventoryDenyReason.Invalid;
                    return false;
            }
        }

        // ---------- 포션 꺼내기 (손에 들기) ----------

        /// <summary>
        /// 한 번에 꺼낼 수 있는 최대 포션 수입니다. (양손)
        /// </summary>
        public const int MaxTakeOutCount = 2;

        private System.Random _random = new System.Random();

        /// <summary>
        /// 무작위 꺼내기에 쓸 난수 생성기를 바꿉니다. 같은 결과가 필요한 시험에서 시드를 고정할 때 사용합니다.
        /// </summary>
        public void SetRandom(System.Random random)
        {
            _random = random ?? new System.Random();
        }

        /// <summary>
        /// 포션 꺼내기를 검증합니다. 꺼낸 포션은 인벤토리에서 사라지고, 요청한 쪽이 결과(Outcome.Potions)로 받아 손에 듭니다.
        /// - 칸 지정(From.Slot 이 0 이상): 내 개인 인벤토리의 그 칸(퀵슬롯)에 든 포션 한 개를 꺼냅니다. 비었거나 재료이면 거부합니다.
        /// - 무작위(From.Slot 이 -1): 공용 인벤에서 Count 개(1~2)를 무작위로 꺼내고, 공용 인벤에 포션이 모자라면 모자란 만큼 내 개인 인벤토리에서 무작위로 꺼냅니다.
        ///   하나도 못 꺼내면 NotFound 로 거부합니다. (손이 비어 있는 만큼만 Count 로 요청하면 됩니다)
        /// 무작위는 마스터가 정하고 결과가 변경 목록에 담겨 전원에게 가므로 모든 사본이 같은 결과가 됩니다.
        /// </summary>
        private bool ValidateTakeOut(InventoryCommand request, int actor, ref InventoryCommand apply, out InventoryDenyReason reason)
        {
            reason = InventoryDenyReason.None;
            var ctx = new Context(this);
            PersonalInventory personal = ctx.Slots(ContainerRef.Personal(actor));

            // 칸 지정 (퀵슬롯)
            if (request.From.Slot >= 0)
            {
                int slot = request.From.Slot;

                if (slot >= personal.Capacity)
                {
                    reason = InventoryDenyReason.Invalid;
                    return false;
                }

                if (personal.Slots[slot].IsEmpty)
                {
                    reason = InventoryDenyReason.SlotEmpty;
                    return false;
                }

                if (personal.Slots[slot].Kind != InventorySlotKind.Potion)
                {
                    reason = InventoryDenyReason.Invalid;   // 재료는 손에 들 수 없습니다.
                    return false;
                }

                personal.TakeSlot(slot);
                apply.Count = 1;
                apply.Changes = ctx.Flush();
                return true;
            }

            // 무작위
            if (request.Count < 1 || request.Count > MaxTakeOutCount)
            {
                reason = InventoryDenyReason.Invalid;
                return false;
            }

            var packCandidates = new List<int>();
            IReadOnlyList<PotionInstance> packPotions = Pack.Potions.Items;
            for (int i = 0; i < packPotions.Count; i++)
            {
                packCandidates.Add(packPotions[i].InstanceId);
            }

            var personalCandidates = new List<int>();
            for (int i = 0; i < personal.Capacity; i++)
            {
                if (personal.Slots[i].Kind == InventorySlotKind.Potion)
                {
                    personalCandidates.Add(i);
                }
            }

            int taken = 0;
            while (taken < request.Count)
            {
                if (packCandidates.Count > 0)
                {
                    int pick = _random.Next(packCandidates.Count);
                    ctx.RemovePotion(ContainerKind.Pack, packCandidates[pick]);
                    packCandidates.RemoveAt(pick);
                }
                else if (personalCandidates.Count > 0)
                {
                    int pick = _random.Next(personalCandidates.Count);
                    personal.TakeSlot(personalCandidates[pick]);
                    personalCandidates.RemoveAt(pick);
                }
                else
                {
                    break;
                }

                taken++;
            }

            if (taken == 0)
            {
                reason = InventoryDenyReason.NotFound;   // 꺼낼 포션이 없음
                return false;
            }

            apply.Count = taken;
            apply.Changes = ctx.Flush();
            return true;
        }

        /// <summary>
        /// 꺼내기 변경으로 인벤토리에서 빠질 포션들을 적용하기 전에 모아 둡니다. (적용하고 나면 포션 객체를 찾을 수 없으므로)
        /// </summary>
        private List<PotionInstance> CollectTakenPotions(List<InventoryChange> changes)
        {
            var taken = new List<PotionInstance>();

            if (changes == null)
            {
                return taken;
            }

            for (int i = 0; i < changes.Count; i++)
            {
                InventoryChange change = changes[i];

                if (change.Type == InventoryChangeType.RemovePotion && change.Target.IsStore)
                {
                    PotionInventory potions = GetStore(change.Target.Kind).Potions;
                    int index = potions.IndexOf(change.InstanceId);

                    if (index >= 0)
                    {
                        taken.Add(potions.Items[index]);
                    }
                }
                else if (change.Type == InventoryChangeType.SetSlot && change.Slot.IsEmpty)
                {
                    PersonalInventory inventory = FindSlotInventory(change.Target);

                    if (inventory != null && change.Target.Slot >= 0 && change.Target.Slot < inventory.Capacity
                        && inventory.Slots[change.Target.Slot].Kind == InventorySlotKind.Potion)
                    {
                        taken.Add(inventory.Slots[change.Target.Slot].Potion);
                    }
                }
            }

            return taken;
        }

        // ---------- 사망, 개발용 생성 ----------

        /// <summary>
        /// 개인 인벤토리의 내용을 시체 상자로 옮기는 변경을 만듭니다. removePersonal 이면 개인 인벤토리 자체도 없앱니다. (방을 나간 경우)
        /// </summary>
        private bool BuildDie(int actor, Vector3 position, bool removePersonal, ref InventoryCommand apply, out InventoryDenyReason reason)
        {
            reason = InventoryDenyReason.None;
            _personals.TryGetValue(actor, out PersonalInventory inventory);

            bool hasItem = false;
            if (inventory != null)
            {
                for (int i = 0; i < inventory.Capacity; i++)
                {
                    if (!inventory.Slots[i].IsEmpty)
                    {
                        hasItem = true;
                        break;
                    }
                }
            }

            if (!hasItem && !removePersonal)
            {
                reason = InventoryDenyReason.Invalid;   // 남길 것이 없습니다.
                return false;
            }

            var changes = new List<InventoryChange>();

            if (hasItem)
            {
                int corpseId = _nextCorpseId++;
                changes.Add(InventoryChange.CreateCorpse(corpseId, position, inventory.Capacity));

                for (int i = 0; i < inventory.Capacity; i++)
                {
                    if (inventory.Slots[i].IsEmpty)
                    {
                        continue;
                    }

                    changes.Add(InventoryChange.SetSlot(ContainerRef.Corpse(corpseId, i), inventory.Slots[i]));

                    if (!removePersonal)
                    {
                        changes.Add(InventoryChange.SetSlot(ContainerRef.Personal(actor, i), default));
                    }
                }

                apply.CorpseId = corpseId;
            }

            if (removePersonal && inventory != null)
            {
                changes.Add(InventoryChange.RemovePersonal(actor));
            }

            apply.Changes = changes;
            return true;
        }

        private bool ValidateGrant(InventoryCommand request, int actor, ref InventoryCommand apply, out InventoryDenyReason reason)
        {
            if (!AllowGrant)
            {
                reason = InventoryDenyReason.NoAccess;
                return false;
            }

            if (!CheckAccess(request.To, actor, false, out reason))
            {
                return false;
            }

            var ctx = new Context(this);
            InventorySlot item;

            if (request.PotionId > 0)
            {
                if (!_data.TryGetPotion(request.PotionId, out PotionData data))
                {
                    reason = InventoryDenyReason.Invalid;
                    return false;
                }

                TrapPotionKeyword keyword = null;
                if (request.KeywordId != 0 && !_data.TryGetKeyword(request.KeywordId, out keyword))
                {
                    reason = InventoryDenyReason.Invalid;
                    return false;
                }

                var potion = new PotionInstance(data, keyword, PotionInstanceIds.Next());
                item = InventorySlot.OfPotion(potion);
                apply.InstanceId = potion.InstanceId;
            }
            else
            {
                if (request.MaterialId <= 0 || request.Count <= 0 || !_data.IsKnownMaterial(request.MaterialId))
                {
                    reason = InventoryDenyReason.Invalid;
                    return false;
                }

                item = InventorySlot.OfMaterial(request.MaterialId, request.Count);
            }

            if (!TryPlace(ctx, request.To, item, out int moved, out reason))
            {
                return false;
            }

            apply.Count = moved;
            apply.Changes = ctx.Flush();
            return true;
        }

        // ---------- 공통 검증 ----------

        /// <summary>
        /// 이 장소를 요청한 사람이 쓸 수 있는지 확인합니다. 남의 개인 인벤토리와 베이스캠프 밖의 창고는 거부합니다.
        /// </summary>
        private bool CheckAccess(ContainerRef container, int actor, bool isSource, out InventoryDenyReason reason)
        {
            reason = InventoryDenyReason.None;

            switch (container.Kind)
            {
                case ContainerKind.Pack:
                    return true;

                case ContainerKind.Storage:
                    if (_storageAccessGate != null && !_storageAccessGate(actor))
                    {
                        reason = InventoryDenyReason.NoAccess;
                        return false;
                    }
                    return true;

                case ContainerKind.Personal:
                    if (container.Id != actor)
                    {
                        reason = InventoryDenyReason.NoAccess;
                        return false;
                    }
                    return true;

                case ContainerKind.Corpse:
                    if (!_corpses.ContainsKey(container.Id))
                    {
                        reason = InventoryDenyReason.NotFound;
                        return false;
                    }
                    return true;

                case ContainerKind.Ground:
                    if (!isSource)
                    {
                        reason = InventoryDenyReason.Invalid;   // 바닥에는 놓을 수 없습니다.
                        return false;
                    }

                    if (!_ground.ContainsKey(container.Id))
                    {
                        reason = InventoryDenyReason.NotFound;
                        return false;
                    }
                    return true;

                default:
                    reason = InventoryDenyReason.Invalid;
                    return false;
            }
        }

        private ItemStore GetStore(ContainerKind kind)
        {
            return kind == ContainerKind.Storage ? Storage : Pack;
        }

        /// <summary>
        /// 칸 구조 장소(개인 인벤토리, 시체 상자)의 실제 인벤토리를 찾습니다. 없으면 null 입니다.
        /// </summary>
        private PersonalInventory FindSlotInventory(ContainerRef container)
        {
            if (container.Kind == ContainerKind.Personal)
            {
                return _personals.TryGetValue(container.Id, out PersonalInventory personal) ? personal : null;
            }

            if (container.Kind == ContainerKind.Corpse)
            {
                return _corpses.TryGetValue(container.Id, out CorpseBox corpse) ? corpse.Slots : null;
            }

            return null;
        }

        private static int FindEmptySlot(PersonalInventory inventory)
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                if (inventory.Slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool SlotEquals(InventorySlot a, InventorySlot b)
        {
            if (a.Kind != b.Kind || a.MaterialId != b.MaterialId || a.Count != b.Count)
            {
                return false;
            }

            if (a.Potion == null || b.Potion == null)
            {
                return a.Potion == null && b.Potion == null;
            }

            return a.Potion.InstanceId == b.Potion.InstanceId;
        }

        /// <summary>
        /// 검증 한 번 동안의 "해 보기" 기록입니다. 칸 구조 장소는 복사본에 해 보고 원본과의 차이를 변경으로 만들고,
        /// 재료 총량과 포션 목록은 변화량으로 모았다가 변경으로 만듭니다. 실제 사본은 바꾸지 않습니다.
        /// </summary>
        private sealed class Context
        {
            private readonly TeamInventoryState _state;
            private readonly Dictionary<long, PersonalInventory> _clones = new Dictionary<long, PersonalInventory>();
            private readonly Dictionary<long, ContainerRef> _refs = new Dictionary<long, ContainerRef>();
            private readonly Dictionary<(ContainerKind, int), int> _materialDelta = new Dictionary<(ContainerKind, int), int>();
            private readonly List<(ContainerKind, int)> _removedPotions = new List<(ContainerKind, int)>();
            private readonly List<(ContainerKind, PotionInstance)> _addedPotions = new List<(ContainerKind, PotionInstance)>();

            public readonly List<InventoryChange> Changes = new List<InventoryChange>();

            public Context(TeamInventoryState state)
            {
                _state = state;
            }

            private static long Key(ContainerRef container)
            {
                return ((long)(byte)container.Kind << 32) | (uint)container.Id;
            }

            /// <summary>
            /// 칸 구조 장소의 복사본입니다. 처음 부를 때 만들고, 이후에는 같은 복사본을 돌려줍니다.
            /// 아직 없는 개인 인벤토리는 빈 복사본입니다.
            /// </summary>
            public PersonalInventory Slots(ContainerRef container)
            {
                long key = Key(container);

                if (_clones.TryGetValue(key, out PersonalInventory clone))
                {
                    return clone;
                }

                PersonalInventory original = _state.FindSlotInventory(container);
                clone = original != null
                    ? original.Clone()
                    : new PersonalInventory(PersonalInventory.DefaultCapacity, _state._data.GetMaterialLimit);

                _clones[key] = clone;
                _refs[key] = container;
                return clone;
            }

            public int MaterialDelta(ContainerKind kind, int materialId)
            {
                return _materialDelta.TryGetValue((kind, materialId), out int delta) ? delta : 0;
            }

            public void AddMaterialDelta(ContainerKind kind, int materialId, int delta)
            {
                _materialDelta[(kind, materialId)] = MaterialDelta(kind, materialId) + delta;
            }

            public bool IsPotionRemoved(ContainerKind kind, int instanceId)
            {
                return _removedPotions.Contains((kind, instanceId));
            }

            public void RemovePotion(ContainerKind kind, int instanceId)
            {
                _removedPotions.Add((kind, instanceId));
            }

            public void AddPotion(ContainerKind kind, PotionInstance potion)
            {
                _addedPotions.Add((kind, potion));
            }

            /// <summary>
            /// 지금까지의 "해 보기"를 변경 목록으로 만들어 돌려줍니다. 이미 모아 둔 변경(Changes) 뒤에 이어 붙입니다.
            /// </summary>
            public List<InventoryChange> Flush()
            {
                foreach (KeyValuePair<(ContainerKind, int), int> entry in _materialDelta)
                {
                    if (entry.Value == 0)
                    {
                        continue;
                    }

                    ContainerKind kind = entry.Key.Item1;
                    int materialId = entry.Key.Item2;
                    int current = _state.GetStore(kind).Materials.GetCount(materialId);
                    ContainerRef target = kind == ContainerKind.Storage ? ContainerRef.StorageRef : ContainerRef.PackRef;
                    Changes.Add(InventoryChange.SetMaterial(target, materialId, current + entry.Value));
                }

                for (int i = 0; i < _removedPotions.Count; i++)
                {
                    ContainerRef target = _removedPotions[i].Item1 == ContainerKind.Storage ? ContainerRef.StorageRef : ContainerRef.PackRef;
                    Changes.Add(InventoryChange.RemovePotion(target, _removedPotions[i].Item2));
                }

                for (int i = 0; i < _addedPotions.Count; i++)
                {
                    ContainerRef target = _addedPotions[i].Item1 == ContainerKind.Storage ? ContainerRef.StorageRef : ContainerRef.PackRef;
                    Changes.Add(InventoryChange.AddPotion(target, _addedPotions[i].Item2));
                }

                foreach (KeyValuePair<long, PersonalInventory> entry in _clones)
                {
                    ContainerRef container = _refs[entry.Key];
                    PersonalInventory original = _state.FindSlotInventory(container);
                    PersonalInventory clone = entry.Value;
                    bool allEmpty = true;

                    for (int i = 0; i < clone.Capacity; i++)
                    {
                        InventorySlot before = original != null ? original.Slots[i] : default;
                        InventorySlot after = clone.Slots[i];

                        if (!after.IsEmpty)
                        {
                            allEmpty = false;
                        }

                        if (!SlotEquals(before, after))
                        {
                            ContainerRef slotRef = container;
                            slotRef.Slot = i;
                            Changes.Add(InventoryChange.SetSlot(slotRef, after));
                        }
                    }

                    // 시체 상자가 비게 되면 상자도 없앱니다.
                    if (container.Kind == ContainerKind.Corpse && allEmpty && original != null)
                    {
                        Changes.Add(InventoryChange.RemoveCorpse(container.Id));
                    }
                }

                return Changes;
            }
        }

        // ==================== 모든 클라이언트: 적용 ============================

        /// <summary>
        /// 마스터가 확정한 적용 명령(변경 목록)을 사본에 적용합니다. (마스터 포함 모든 클라이언트)
        /// 버전이 이어지지 않거나 사본이 어긋나 있으면 적용하지 않고 상태로 알려 줍니다. 그때는 전체 목록을 다시 받아야 합니다.
        /// 변경은 모두 절대값이라, 같은 적용은 어디서나 같은 결과를 냅니다. 이벤트는 전부 적용한 뒤에 발생합니다.
        /// </summary>
        public InventoryApplyStatus Apply(InventoryCommand apply, out InventoryOutcome outcome)
        {
            outcome = default;

            if (apply.Version <= Version)
            {
                return InventoryApplyStatus.Ignored;
            }

            if (apply.Version != Version + 1)
            {
                return InventoryApplyStatus.VersionGap;
            }

            List<InventoryChange> changes = apply.Changes;
            bool consistent = true;

            // 꺼내기는 빠질 포션을 적용하기 전에 모아 둡니다.
            List<PotionInstance> takenPotions = apply.Op == InventoryOp.TakeOut ? CollectTakenPotions(changes) : null;

            if (changes != null)
            {
                for (int i = 0; i < changes.Count; i++)
                {
                    if (!ApplyChange(changes[i]))
                    {
                        consistent = false;
                        break;
                    }
                }
            }

            FireDeferredEvents();

            if (!consistent)
            {
                return InventoryApplyStatus.Inconsistent;
            }

            Version = apply.Version;

            outcome.Value = apply.Count;
            outcome.CorpseId = apply.CorpseId;
            outcome.Changes = changes ?? new List<InventoryChange>();
            outcome.Potion = FindCreatedPotion(changes, apply.InstanceId);
            outcome.Potions = takenPotions;
            return InventoryApplyStatus.Applied;
        }

        private bool ApplyChange(InventoryChange change)
        {
            switch (change.Type)
            {
                case InventoryChangeType.SetMaterial:
                    if (!change.Target.IsStore)
                    {
                        return false;
                    }

                    GetStore(change.Target.Kind).Materials.SetCount(change.MaterialId, change.Count);
                    return true;

                case InventoryChangeType.AddPotion:
                    return change.Target.IsStore && change.Potion != null && GetStore(change.Target.Kind).Potions.Add(change.Potion);

                case InventoryChangeType.RemovePotion:
                    return change.Target.IsStore && GetStore(change.Target.Kind).Potions.Take(change.InstanceId) != null;

                case InventoryChangeType.SetSlot:
                    return ApplySetSlot(change);

                case InventoryChangeType.CreateCorpse:
                    if (_corpses.ContainsKey(change.Id) || change.Capacity <= 0 || change.Capacity > MaxCorpseSlots)
                    {
                        return false;
                    }

                    _corpses.Add(change.Id, new CorpseBox(change.Id, change.Position, new InventorySlot[change.Capacity], _data.GetMaterialLimit));
                    _nextCorpseId = Math.Max(_nextCorpseId, change.Id + 1);
                    _createdCorpses.Add(change.Id);
                    return true;

                case InventoryChangeType.RemoveCorpse:
                    if (!_corpses.Remove(change.Id))
                    {
                        return false;
                    }

                    _createdCorpses.Remove(change.Id);
                    _changedCorpses.Remove(change.Id);
                    OnCorpseRemoved?.Invoke(change.Id);
                    return true;

                case InventoryChangeType.RemovePersonal:
                    if (!_personals.Remove(change.Id))
                    {
                        return false;
                    }

                    OnPersonalRemoved?.Invoke(change.Id);
                    return true;

                case InventoryChangeType.CreateGround:
                    if (_ground.ContainsKey(change.Id) || change.Count <= 0)
                    {
                        return false;
                    }

                    _ground.Add(change.Id, new GroundDrop(change.Id, change.MaterialId, change.Count, change.Position));
                    _nextDropId = Math.Max(_nextDropId, change.Id + 1);
                    _createdDrops.Add(change.Id);
                    return true;

                case InventoryChangeType.SetGround:
                    if (!_ground.TryGetValue(change.Id, out GroundDrop drop))
                    {
                        return false;
                    }

                    if (change.Count <= 0)
                    {
                        _ground.Remove(change.Id);
                        _createdDrops.Remove(change.Id);
                        _changedDrops.Remove(change.Id);
                        OnGroundRemoved?.Invoke(change.Id);
                    }
                    else
                    {
                        drop.Count = change.Count;
                        _changedDrops.Add(change.Id);
                    }
                    return true;

                default:
                    return false;
            }
        }

        private bool ApplySetSlot(InventoryChange change)
        {
            ContainerRef target = change.Target;

            if (target.Kind == ContainerKind.Personal)
            {
                return GetOrCreatePersonal(target.Id).SetSlot(target.Slot, change.Slot);
            }

            if (target.Kind == ContainerKind.Corpse && _corpses.TryGetValue(target.Id, out CorpseBox corpse))
            {
                if (!corpse.Slots.SetSlot(target.Slot, change.Slot))
                {
                    return false;
                }

                _changedCorpses.Add(target.Id);
                return true;
            }

            return false;
        }

        private PersonalInventory GetOrCreatePersonal(int actor)
        {
            if (!_personals.TryGetValue(actor, out PersonalInventory inventory))
            {
                inventory = new PersonalInventory(PersonalInventory.DefaultCapacity, _data.GetMaterialLimit);
                _personals.Add(actor, inventory);
                OnPersonalAdded?.Invoke(actor);
            }

            return inventory;
        }

        private void FireDeferredEvents()
        {
            foreach (int id in _createdCorpses)
            {
                OnCorpseCreated?.Invoke(id);
            }

            foreach (int id in _changedCorpses)
            {
                if (!_createdCorpses.Contains(id))
                {
                    OnCorpseChanged?.Invoke(id);
                }
            }

            foreach (int id in _createdDrops)
            {
                OnGroundAdded?.Invoke(id);
            }

            foreach (int id in _changedDrops)
            {
                if (!_createdDrops.Contains(id))
                {
                    OnGroundChanged?.Invoke(id);
                }
            }

            _createdCorpses.Clear();
            _changedCorpses.Clear();
            _createdDrops.Clear();
            _changedDrops.Clear();
        }

        /// <summary>
        /// 이번 변경으로 새로 만들어진 포션(Craft, Grant)을 변경 목록에서 찾습니다.
        /// </summary>
        private static PotionInstance FindCreatedPotion(List<InventoryChange> changes, int instanceId)
        {
            if (changes == null || instanceId <= 0)
            {
                return null;
            }

            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i].Type == InventoryChangeType.AddPotion && changes[i].Potion != null && changes[i].Potion.InstanceId == instanceId)
                {
                    return changes[i].Potion;
                }

                if (changes[i].Type == InventoryChangeType.SetSlot && changes[i].Slot.Kind == InventorySlotKind.Potion
                    && changes[i].Slot.Potion != null && changes[i].Slot.Potion.InstanceId == instanceId)
                {
                    return changes[i].Slot.Potion;
                }
            }

            return null;
        }

        // ==================== 스냅샷 (전체 목록) ============================

        /// <summary>
        /// 전체 상태를 하나의 byte 배열로 묶습니다. 늦게 들어온 사람이나 사본이 어긋난 사람에게 마스터가 보냅니다.
        /// 형식 : 메시지 버전, 사본 버전, 다음 시체 ID, 다음 주머니 ID, 공용 인벤(재료, 포션), 창고(재료, 포션),
        ///        개인 인벤들(번호 + 칸), 시체 상자들(ID, 위치, 칸), 바닥 주머니들(ID, 재료, 개수, 위치)
        /// </summary>
        public byte[] CreateSnapshot()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(InventoryMessages.Version);
                writer.Write(Version);
                writer.Write(_nextCorpseId);
                writer.Write(_nextDropId);

                WriteBlock(writer, InventoryPacker.PackMaterials(Pack.Materials.Counts));
                WriteBlock(writer, InventoryPacker.PackPotions(Pack.Potions.Items));
                WriteBlock(writer, InventoryPacker.PackMaterials(Storage.Materials.Counts));
                WriteBlock(writer, InventoryPacker.PackPotions(Storage.Potions.Items));

                writer.Write(_personals.Count);

                foreach (KeyValuePair<int, PersonalInventory> entry in _personals)
                {
                    writer.Write(entry.Key);
                    WriteBlock(writer, InventoryPacker.PackSlots(entry.Value.Slots));
                }

                writer.Write(_corpses.Count);

                foreach (KeyValuePair<int, CorpseBox> entry in _corpses)
                {
                    CorpseBox box = entry.Value;
                    writer.Write(box.Id);
                    writer.Write(box.Position.x);
                    writer.Write(box.Position.y);
                    writer.Write(box.Position.z);
                    WriteBlock(writer, InventoryPacker.PackSlots(box.Slots.Slots));
                }

                writer.Write(_ground.Count);

                foreach (KeyValuePair<int, GroundDrop> entry in _ground)
                {
                    GroundDrop drop = entry.Value;
                    writer.Write(drop.Id);
                    writer.Write(checked((ushort)drop.MaterialId));
                    writer.Write(checked((ushort)drop.Count));
                    writer.Write(drop.Position.x);
                    writer.Write(drop.Position.y);
                    writer.Write(drop.Position.z);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 스냅샷으로 전체 상태를 통째로 바꿉니다. 끝까지 읽어서 문제가 없을 때만 바꾸므로, 실패하면 기존 상태가 그대로 남습니다.
        /// </summary>
        public bool TryLoadSnapshot(byte[] data)
        {
            if (data == null || data.Length < 13)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadByte() != InventoryMessages.Version)
                    {
                        return false;
                    }

                    int version = reader.ReadInt32();
                    int nextCorpseId = reader.ReadInt32();
                    int nextDropId = reader.ReadInt32();

                    if (!InventoryPacker.TryUnpackMaterials(ReadBlock(reader), out Dictionary<int, int> packMaterials)
                        || !InventoryPacker.TryUnpackPotions(ReadBlock(reader), out List<PotionInstance> packPotions)
                        || !InventoryPacker.TryUnpackMaterials(ReadBlock(reader), out Dictionary<int, int> storageMaterials)
                        || !InventoryPacker.TryUnpackPotions(ReadBlock(reader), out List<PotionInstance> storagePotions))
                    {
                        return false;
                    }

                    int personalCount = reader.ReadInt32();
                    if (personalCount < 0 || personalCount > 1000)
                    {
                        return false;
                    }

                    var personals = new List<KeyValuePair<int, InventorySlot[]>>(personalCount);

                    for (int i = 0; i < personalCount; i++)
                    {
                        int actor = reader.ReadInt32();

                        if (!InventoryPacker.TryUnpackSlots(ReadBlock(reader), out InventorySlot[] slots))
                        {
                            return false;
                        }

                        personals.Add(new KeyValuePair<int, InventorySlot[]>(actor, slots));
                    }

                    int corpseCount = reader.ReadInt32();
                    if (corpseCount < 0 || corpseCount > 100000)
                    {
                        return false;
                    }

                    var corpses = new List<CorpseBox>(corpseCount);

                    for (int i = 0; i < corpseCount; i++)
                    {
                        int id = reader.ReadInt32();
                        var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                        if (!InventoryPacker.TryUnpackSlots(ReadBlock(reader), out InventorySlot[] slots))
                        {
                            return false;
                        }

                        corpses.Add(new CorpseBox(id, position, slots, _data.GetMaterialLimit));
                    }

                    int dropCount = reader.ReadInt32();
                    if (dropCount < 0 || dropCount > 100000)
                    {
                        return false;
                    }

                    var drops = new List<GroundDrop>(dropCount);

                    for (int i = 0; i < dropCount; i++)
                    {
                        int id = reader.ReadInt32();
                        int materialId = reader.ReadUInt16();
                        int count = reader.ReadUInt16();
                        var position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                        if (count <= 0 || !_data.IsKnownMaterial(materialId))
                        {
                            return false;
                        }

                        drops.Add(new GroundDrop(id, materialId, count, position));
                    }

                    if (stream.Position != stream.Length)
                    {
                        return false;
                    }

                    // 여기까지 문제가 없었을 때만 상태를 바꿉니다.
                    LoadStore(Pack, packMaterials, packPotions);
                    LoadStore(Storage, storageMaterials, storagePotions);

                    _personals.Clear();
                    for (int i = 0; i < personals.Count; i++)
                    {
                        var inventory = new PersonalInventory(personals[i].Value.Length, _data.GetMaterialLimit);
                        inventory.Load(personals[i].Value);
                        _personals[personals[i].Key] = inventory;
                    }

                    _corpses.Clear();
                    for (int i = 0; i < corpses.Count; i++)
                    {
                        _corpses[corpses[i].Id] = corpses[i];
                    }

                    _ground.Clear();
                    for (int i = 0; i < drops.Count; i++)
                    {
                        _ground[drops[i].Id] = drops[i];
                    }

                    _nextCorpseId = Math.Max(1, nextCorpseId);
                    _nextDropId = Math.Max(1, nextDropId);
                    Version = version;
                    OnReloaded?.Invoke();
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        private static void LoadStore(ItemStore store, Dictionary<int, int> materials, List<PotionInstance> potions)
        {
            store.Materials.Clear();
            foreach (KeyValuePair<int, int> entry in materials)
            {
                store.Materials.SetCount(entry.Key, entry.Value);
            }

            store.Potions.Clear();
            for (int i = 0; i < potions.Count; i++)
            {
                store.Potions.Add(potions[i]);
            }
        }

        // 길이(int) + 내용. 스냅샷 안에서 묶음 여러 개를 이어 붙일 때 사용합니다.
        private static void WriteBlock(BinaryWriter writer, byte[] block)
        {
            writer.Write(block.Length);
            writer.Write(block);
        }

        private static byte[] ReadBlock(BinaryReader reader)
        {
            int length = reader.ReadInt32();

            // 남은 byte 보다 큰 길이는 깨진 데이터입니다. (BinaryReader 는 모자라면 짧게 돌려주므로 직접 확인)
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                throw new EndOfStreamException();
            }

            return reader.ReadBytes(length);
        }
    }
}
