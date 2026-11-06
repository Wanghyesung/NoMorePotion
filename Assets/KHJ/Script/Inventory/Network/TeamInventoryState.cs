using System;
using System.Collections.Generic;
using System.IO;
using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 팀이 공유하는 인벤토리 상태(재료, 포션, 시체 상자들)의 사본입니다. 모든 클라이언트가 같은 사본을 하나씩 가집니다.
    /// 네트워크 라이브러리를 모르는 순수 C# 클래스라서, 마스터 한 개와 클라이언트 여러 개를 흉내 내어 에디터에서 시험할 수 있습니다.
    ///
    /// 흐름
    /// 1) 클라이언트가 요청(InventoryCommand)을 마스터에게 보냅니다.
    /// 2) 마스터가 <see cref="TryValidate"/> 로 검증합니다. 이때는 사본을 바꾸지 않고, 확정된 값(실제 개수, 부여한 ID 등)이 담긴 적용 명령만 만듭니다.
    ///    거부되면 이유를 요청한 사람에게만 알립니다.
    /// 3) 적용 명령을 마스터를 포함한 모든 클라이언트가 <see cref="Apply"/> 로 똑같이 적용합니다.
    /// 마스터가 직접 바꾸지 않고 적용 메시지를 받아서 바꾸는 이유는, 마스터도 같은 경로를 쓰게 해서 사본이 어긋날 틈을 없애기 위해서입니다.
    ///
    /// 모든 적용 명령에는 사본 버전이 붙습니다. 번호가 이어지지 않으면(중간 메시지를 놓침) 적용하지 않고 VersionGap 을 알려서
    /// 전체 목록(스냅샷)을 다시 받게 합니다.
    /// </summary>
    public sealed class TeamInventoryState
    {
        /// <summary>
        /// 시체 상자의 칸 수는 byte 1개로 보내므로 255칸이 한계입니다.
        /// </summary>
        public const int MaxCorpseSlots = 255;

        private readonly Dictionary<int, CorpseBox> _corpses = new Dictionary<int, CorpseBox>();

        // 시체 상자 ID 발급 번호입니다. 마스터만 발급하며, 스냅샷에 담아 두어 마스터가 바뀌어도 이어 쓸 수 있게 합니다.
        private int _nextCorpseId = 1;

        public TeamInventoryState(MaterialInventory materials = null, PotionInventory potions = null)
        {
            Materials = materials ?? new MaterialInventory();
            Potions = potions ?? new PotionInventory();
        }

        public MaterialInventory Materials { get; }

        public PotionInventory Potions { get; }

        /// <summary>
        /// 시체 상자들입니다. (읽기 전용)
        /// </summary>
        public IReadOnlyDictionary<int, CorpseBox> Corpses => _corpses;

        /// <summary>
        /// 마지막으로 적용한 사본 버전입니다. 마스터가 적용할 때마다 1씩 오릅니다.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// 시체 상자가 새로 생겼을 때 그 ID 와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnCorpseCreated;

        /// <summary>
        /// 시체 상자의 내용이 바뀌었을 때(꺼냄, 넣음) 그 ID 와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnCorpseChanged;

        /// <summary>
        /// 스냅샷으로 전체 상태가 통째로 바뀌었을 때 발생합니다. (보여 주는 쪽이 처음부터 다시 그려야 함)
        /// </summary>
        public event Action OnReloaded;

        // ==================== 마스터: 요청 검증 ============================

        /// <summary>
        /// 요청을 검증합니다. (마스터에서만 호출) 사본은 바꾸지 않습니다.
        /// 통과하면 모든 클라이언트가 적용할 명령(apply)을 만들고, 거부하면 이유(reason)를 돌려줍니다.
        /// </summary>
        /// <param name="requesterActor">요청한 사람의 ActorNumber</param>
        public bool TryValidate(InventoryCommand request, int requesterActor, out InventoryCommand apply, out InventoryDenyReason reason)
        {
            apply = request;
            apply.RequesterActor = requesterActor;
            apply.Version = Version + 1;
            reason = InventoryDenyReason.None;

            PotionDataManager manager = PotionDataManager.Instance;

            switch (request.Op)
            {
                case InventoryOp.AddMaterial:
                {
                    if (request.MaterialId <= 0 || request.Count <= 0 || !IsKnownMaterial(request.MaterialId))
                    {
                        reason = InventoryDenyReason.Invalid;
                        return false;
                    }

                    int accepted = Math.Min(request.Count, Materials.GetRoom(request.MaterialId));
                    if (accepted <= 0)
                    {
                        reason = InventoryDenyReason.Full;
                        return false;
                    }

                    apply.Count = accepted;
                    apply.NewTotal = Materials.GetCount(request.MaterialId) + accepted;
                    return true;
                }

                case InventoryOp.RemoveMaterial:
                {
                    if (request.MaterialId <= 0 || request.Count <= 0)
                    {
                        reason = InventoryDenyReason.Invalid;
                        return false;
                    }

                    int current = Materials.GetCount(request.MaterialId);
                    if (current < request.Count)
                    {
                        reason = InventoryDenyReason.NotEnough;
                        return false;
                    }

                    apply.NewTotal = current - request.Count;
                    return true;
                }

                case InventoryOp.AddPotion:
                {
                    if (manager == null || !manager.TryGetPotionData(request.PotionId, out PotionData data))
                    {
                        reason = InventoryDenyReason.Invalid;
                        return false;
                    }

                    TrapPotionKeyword keyword = null;
                    if (request.KeywordId != 0 && !manager.TryGetTrapKeyword(request.KeywordId, out keyword))
                    {
                        reason = InventoryDenyReason.Invalid;
                        return false;
                    }

                    if (Potions.IsFull)
                    {
                        reason = InventoryDenyReason.Full;
                        return false;
                    }

                    // 인스턴스 ID 는 마스터만 발급합니다. 검증을 통과한 뒤에 발급해서 번호를 낭비하지 않습니다.
                    apply.Potion = new PotionInstance(data, keyword, PotionInstanceIds.Next());
                    apply.InstanceId = apply.Potion.InstanceId;
                    return true;
                }

                case InventoryOp.TakePotion:
                {
                    if (!Potions.Contains(request.InstanceId))
                    {
                        reason = InventoryDenyReason.NotFound;
                        return false;
                    }

                    return true;
                }

                case InventoryOp.CreateCorpse:
                {
                    if (request.Slots == null || request.Slots.Length == 0 || request.Slots.Length > MaxCorpseSlots || !HasAnyItem(request.Slots))
                    {
                        reason = InventoryDenyReason.Invalid;
                        return false;
                    }

                    apply.CorpseId = _nextCorpseId++;
                    return true;
                }

                case InventoryOp.TakeCorpseSlot:
                {
                    if (!_corpses.TryGetValue(request.CorpseId, out CorpseBox corpse))
                    {
                        reason = InventoryDenyReason.NotFound;
                        return false;
                    }

                    if (request.SlotIndex < 0 || request.SlotIndex >= corpse.Slots.Capacity || corpse.Slots.Slots[request.SlotIndex].IsEmpty)
                    {
                        reason = InventoryDenyReason.SlotEmpty;
                        return false;
                    }

                    return true;
                }

                case InventoryOp.PutCorpseSlot:
                {
                    if (!_corpses.TryGetValue(request.CorpseId, out CorpseBox corpse))
                    {
                        reason = InventoryDenyReason.NotFound;
                        return false;
                    }

                    if (!corpse.Slots.PreviewPlace(request.SlotIndex, request.Slot, out InventorySlot placed, out InventorySlot leftover))
                    {
                        reason = InventoryDenyReason.Blocked;
                        return false;
                    }

                    apply.Slot = placed;
                    apply.Leftover = leftover;
                    return true;
                }

                default:
                    reason = InventoryDenyReason.Invalid;
                    return false;
            }
        }

        // ==================== 모든 클라이언트: 적용 ============================

        /// <summary>
        /// 마스터가 확정한 적용 명령을 사본에 적용합니다. (마스터 포함 모든 클라이언트)
        /// 버전이 이어지지 않거나 사본이 어긋나 있으면 적용하지 않고 상태로 알려 줍니다. 그때는 전체 목록을 다시 받아야 합니다.
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

            switch (apply.Op)
            {
                case InventoryOp.AddMaterial:
                case InventoryOp.RemoveMaterial:
                    Materials.SetCount(apply.MaterialId, apply.NewTotal);
                    outcome.Value = apply.Count;
                    break;

                case InventoryOp.AddPotion:
                    if (apply.Potion == null || !Potions.Add(apply.Potion))
                    {
                        return InventoryApplyStatus.Inconsistent;
                    }

                    outcome.Potion = apply.Potion;
                    break;

                case InventoryOp.TakePotion:
                    PotionInstance taken = Potions.Take(apply.InstanceId);
                    if (taken == null)
                    {
                        return InventoryApplyStatus.Inconsistent;
                    }

                    outcome.Potion = taken;
                    break;

                case InventoryOp.CreateCorpse:
                    if (_corpses.ContainsKey(apply.CorpseId))
                    {
                        return InventoryApplyStatus.Inconsistent;
                    }

                    _corpses.Add(apply.CorpseId, new CorpseBox(apply.CorpseId, apply.Position, apply.Slots));
                    _nextCorpseId = Math.Max(_nextCorpseId, apply.CorpseId + 1);
                    outcome.CorpseId = apply.CorpseId;
                    Version = apply.Version;
                    OnCorpseCreated?.Invoke(apply.CorpseId);
                    return InventoryApplyStatus.Applied;

                case InventoryOp.TakeCorpseSlot:
                    if (!_corpses.TryGetValue(apply.CorpseId, out CorpseBox fromBox))
                    {
                        return InventoryApplyStatus.Inconsistent;
                    }

                    InventorySlot content = fromBox.Slots.TakeSlot(apply.SlotIndex);
                    if (content.IsEmpty)
                    {
                        return InventoryApplyStatus.Inconsistent;
                    }

                    outcome.Slot = content;
                    outcome.CorpseId = apply.CorpseId;
                    Version = apply.Version;
                    OnCorpseChanged?.Invoke(apply.CorpseId);
                    return InventoryApplyStatus.Applied;

                case InventoryOp.PutCorpseSlot:
                    if (!_corpses.TryGetValue(apply.CorpseId, out CorpseBox toBox)
                        || !toBox.Slots.TryPlace(apply.SlotIndex, apply.Slot, out InventorySlot _))
                    {
                        return InventoryApplyStatus.Inconsistent;
                    }

                    outcome.Slot = apply.Slot;
                    outcome.Leftover = apply.Leftover;
                    outcome.CorpseId = apply.CorpseId;
                    Version = apply.Version;
                    OnCorpseChanged?.Invoke(apply.CorpseId);
                    return InventoryApplyStatus.Applied;

                default:
                    return InventoryApplyStatus.Inconsistent;
            }

            Version = apply.Version;
            return InventoryApplyStatus.Applied;
        }

        // ==================== 스냅샷 (전체 목록) ============================

        /// <summary>
        /// 전체 상태를 하나의 byte 배열로 묶습니다. 늦게 들어온 사람이나 사본이 어긋난 사람에게 마스터가 보냅니다.
        /// </summary>
        public byte[] CreateSnapshot()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(InventoryMessages.Version);
                writer.Write(Version);
                writer.Write(_nextCorpseId);

                WriteBlock(writer, InventoryPacker.PackMaterials(Materials.Counts));
                WriteBlock(writer, InventoryPacker.PackPotions(Potions.Items));

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

                    if (!InventoryPacker.TryUnpackMaterials(ReadBlock(reader), out Dictionary<int, int> materials)
                        || !InventoryPacker.TryUnpackPotions(ReadBlock(reader), out List<PotionInstance> potions))
                    {
                        return false;
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

                        corpses.Add(new CorpseBox(id, position, slots));
                    }

                    if (stream.Position != stream.Length)
                    {
                        return false;
                    }

                    // 여기까지 문제가 없었을 때만 상태를 바꿉니다.
                    Materials.Clear();
                    foreach (KeyValuePair<int, int> entry in materials)
                    {
                        Materials.SetCount(entry.Key, entry.Value);
                    }

                    Potions.Clear();
                    for (int i = 0; i < potions.Count; i++)
                    {
                        Potions.Add(potions[i]);
                    }

                    _corpses.Clear();
                    for (int i = 0; i < corpses.Count; i++)
                    {
                        _corpses[corpses[i].Id] = corpses[i];
                    }

                    _nextCorpseId = Math.Max(1, nextCorpseId);
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

        // ==================== 내부 ============================

        private static bool IsKnownMaterial(int materialId)
        {
            return PotionDataManager.Instance != null
                && PotionDataManager.Instance.TryGetMaterialData(materialId, out Enzeun.SO.MaterialData _);
        }

        private static bool HasAnyItem(InventorySlot[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    return true;
                }
            }

            return false;
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
