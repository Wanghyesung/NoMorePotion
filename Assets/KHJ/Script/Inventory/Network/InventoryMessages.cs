using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 네트워크 메시지(요청, 적용, 거부)를 byte 배열로 묶고 푸는 정적 도우미입니다.
    /// 네트워크 라이브러리를 모르는 순수 C# 이라서 에디터에서 왕복 시험을 할 수 있습니다.
    /// 형식 (리틀 엔디언, 맨 앞은 버전 1 byte)
    /// - 요청 : 버전, 종류, 요청 번호(int), 종류별 내용
    /// - 적용 : 버전, 종류, 요청 번호(int), 요청한 사람(int), 사본 버전(int), 결과 값(개수, 새 포션 ID, 시체 ID), 변경 목록
    /// - 거부 : 버전, 요청 번호(int), 이유(byte)
    /// 형식이 어긋나면 예외 없이 false 를 반환합니다.
    /// </summary>
    public static class InventoryMessages
    {
        /// <summary>
        /// 현재 형식의 버전입니다. 형식을 바꾸면 올립니다. (스냅샷도 같은 버전을 씁니다)
        /// </summary>
        public const byte Version = 2;

        // 한 적용 메시지에 담을 수 있는 변경의 최대 개수입니다. (깨진 메시지가 큰 메모리를 쓰지 않게)
        private const int MaxChangesPerMessage = 1024;

        // ==================== 요청 (클라이언트 -> 마스터) ============================

        /// <summary>
        /// 요청(마스터가 확정하기 전의 의도)을 byte 로 묶습니다. 값이 byte/ushort 범위를 넘으면 OverflowException 입니다.
        /// </summary>
        public static byte[] PackRequest(InventoryCommand command)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write((byte)command.Op);
                writer.Write(command.RequestId);

                switch (command.Op)
                {
                    case InventoryOp.Grant:
                        WriteContainer(writer, command.To);
                        writer.Write(checked((ushort)command.MaterialId));
                        writer.Write(checked((ushort)command.Count));
                        writer.Write(checked((ushort)command.PotionId));
                        writer.Write(checked((ushort)command.KeywordId));
                        break;

                    case InventoryOp.Transfer:
                        WriteContainer(writer, command.From);
                        WriteContainer(writer, command.To);
                        writer.Write(checked((ushort)command.MaterialId));
                        writer.Write(checked((ushort)command.Count));
                        writer.Write(command.InstanceId);
                        break;

                    case InventoryOp.Craft:
                        WriteIngredient(writer, command.IngredientA);
                        WriteIngredient(writer, command.IngredientB);
                        writer.Write(checked((ushort)command.KeywordId));
                        break;

                    case InventoryOp.Die:
                        WriteVector(writer, command.Position);
                        break;

                    case InventoryOp.TakeOut:
                        // 꺼낼 개수와 개인 인벤토리 칸 번호입니다. 칸 번호 255 는 "칸을 지정하지 않음"(무작위)입니다.
                        writer.Write(checked((byte)command.Count));
                        writer.Write(command.From.Slot < 0 ? (byte)255 : checked((byte)command.From.Slot));
                        break;

                    // Consume 은 종류와 요청 번호만 보냅니다. (어느 칸을 소모할지는 마스터가 정함)
                    case InventoryOp.Consume:
                        break;
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 요청을 풉니다. 형식이 어긋나거나 알 수 없는 종류이면 false 입니다. 마스터 전용 종류(SpawnGround, Leave)는 요청으로 받지 않습니다.
        /// </summary>
        public static bool TryUnpackRequest(byte[] data, out InventoryCommand command)
        {
            command = default;

            if (data == null || data.Length < 6)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadByte() != Version)
                    {
                        return false;
                    }

                    var c = new InventoryCommand
                    {
                        Op = (InventoryOp)reader.ReadByte(),
                        RequestId = reader.ReadInt32(),
                    };

                    switch (c.Op)
                    {
                        case InventoryOp.Grant:
                            if (!TryReadContainer(reader, out c.To))
                            {
                                return false;
                            }

                            c.MaterialId = reader.ReadUInt16();
                            c.Count = reader.ReadUInt16();
                            c.PotionId = reader.ReadUInt16();
                            c.KeywordId = reader.ReadUInt16();
                            break;

                        case InventoryOp.Transfer:
                            if (!TryReadContainer(reader, out c.From) || !TryReadContainer(reader, out c.To))
                            {
                                return false;
                            }

                            c.MaterialId = reader.ReadUInt16();
                            c.Count = reader.ReadUInt16();
                            c.InstanceId = reader.ReadInt32();
                            break;

                        case InventoryOp.Craft:
                            if (!TryReadIngredient(reader, out c.IngredientA) || !TryReadIngredient(reader, out c.IngredientB))
                            {
                                return false;
                            }

                            c.KeywordId = reader.ReadUInt16();
                            break;

                        case InventoryOp.Die:
                            c.Position = ReadVector(reader);
                            break;

                        case InventoryOp.TakeOut:
                        {
                            c.Count = reader.ReadByte();
                            int slot = reader.ReadByte();
                            c.From = new ContainerRef { Kind = ContainerKind.None, Slot = slot == 255 ? -1 : slot };
                            break;
                        }

                        case InventoryOp.Consume:
                            break;

                        default:
                            return false;   // 알 수 없는 종류 또는 마스터 전용 종류
                    }

                    if (stream.Position != stream.Length)
                    {
                        return false;
                    }

                    command = c;
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>
        /// 요청 메시지가 깨졌더라도 맨 앞의 요청 번호만 읽을 수 있으면 읽습니다.
        /// 마스터가 "깨진 요청"을 거부로 바로 알려 줘서, 요청한 쪽이 제한 시간까지 기다리지 않게 하는 데 사용합니다.
        /// </summary>
        public static bool TryPeekRequestId(byte[] data, out int requestId)
        {
            requestId = 0;

            if (data == null || data.Length < 6 || data[0] != Version)
            {
                return false;
            }

            requestId = System.BitConverter.ToInt32(data, 2);
            return true;
        }

        // ==================== 적용 (마스터 -> 전원) ============================

        /// <summary>
        /// 적용(마스터가 확정한 변경 목록과 결과 값)을 byte 로 묶습니다.
        /// </summary>
        public static byte[] PackApply(InventoryCommand command)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write((byte)command.Op);
                writer.Write(command.RequestId);
                writer.Write(command.RequesterActor);
                writer.Write(command.Version);

                writer.Write(command.Count);
                writer.Write(command.InstanceId);
                writer.Write(command.CorpseId);

                List<InventoryChange> changes = command.Changes;
                int changeCount = changes != null ? changes.Count : 0;
                writer.Write(checked((ushort)changeCount));

                for (int i = 0; i < changeCount; i++)
                {
                    WriteChange(writer, changes[i]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 적용을 풉니다. 형식이 어긋나거나 알 수 없는 종류/ID 이면 false 입니다.
        /// </summary>
        public static bool TryUnpackApply(byte[] data, out InventoryCommand command)
        {
            command = default;

            if (data == null || data.Length < 28)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadByte() != Version)
                    {
                        return false;
                    }

                    var c = new InventoryCommand
                    {
                        Op = (InventoryOp)reader.ReadByte(),
                        RequestId = reader.ReadInt32(),
                        RequesterActor = reader.ReadInt32(),
                        Version = reader.ReadInt32(),
                        Count = reader.ReadInt32(),
                        InstanceId = reader.ReadInt32(),
                        CorpseId = reader.ReadInt32(),
                    };

                    int changeCount = reader.ReadUInt16();
                    if (changeCount > MaxChangesPerMessage)
                    {
                        return false;
                    }

                    c.Changes = new List<InventoryChange>(changeCount);

                    for (int i = 0; i < changeCount; i++)
                    {
                        if (!TryReadChange(reader, out InventoryChange change))
                        {
                            return false;
                        }

                        c.Changes.Add(change);
                    }

                    if (stream.Position != stream.Length)
                    {
                        return false;
                    }

                    command = c;
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        // ==================== 거부 (마스터 -> 요청한 사람) ============================

        /// <summary>
        /// 거부(요청 번호 + 이유)를 byte 로 묶습니다.
        /// </summary>
        public static byte[] PackDeny(int requestId, InventoryDenyReason reason)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(requestId);
                writer.Write((byte)reason);

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 거부를 풉니다. 길이나 버전이 맞지 않으면 false 입니다.
        /// </summary>
        public static bool TryUnpackDeny(byte[] data, out int requestId, out InventoryDenyReason reason)
        {
            requestId = 0;
            reason = InventoryDenyReason.None;

            if (data == null || data.Length != 6 || data[0] != Version)
            {
                return false;
            }

            requestId = System.BitConverter.ToInt32(data, 1);
            reason = (InventoryDenyReason)data[5];
            return true;
        }

        // ==================== 변경 한 건 ============================

        // 변경 한 건 : 종류(byte 1) + 종류별 내용
        private static void WriteChange(BinaryWriter writer, InventoryChange change)
        {
            writer.Write((byte)change.Type);

            switch (change.Type)
            {
                case InventoryChangeType.SetMaterial:
                    WriteContainer(writer, change.Target);
                    writer.Write(checked((ushort)change.MaterialId));
                    writer.Write(checked((ushort)change.Count));
                    break;

                case InventoryChangeType.AddPotion:
                    WriteContainer(writer, change.Target);
                    InventoryPacker.WritePotion(writer, change.Potion);
                    break;

                case InventoryChangeType.RemovePotion:
                    WriteContainer(writer, change.Target);
                    writer.Write(change.InstanceId);
                    break;

                case InventoryChangeType.SetSlot:
                    WriteContainer(writer, change.Target);
                    InventoryPacker.WriteSlot(writer, change.Slot);
                    break;

                case InventoryChangeType.CreateCorpse:
                    writer.Write(change.Id);
                    WriteVector(writer, change.Position);
                    writer.Write(checked((byte)change.Capacity));
                    break;

                case InventoryChangeType.RemoveCorpse:
                case InventoryChangeType.RemovePersonal:
                    writer.Write(change.Id);
                    break;

                case InventoryChangeType.CreateGround:
                    writer.Write(change.Id);
                    writer.Write(checked((ushort)change.MaterialId));
                    writer.Write(checked((ushort)change.Count));
                    WriteVector(writer, change.Position);
                    break;

                case InventoryChangeType.SetGround:
                    writer.Write(change.Id);
                    writer.Write(checked((ushort)change.Count));
                    break;
            }
        }

        private static bool TryReadChange(BinaryReader reader, out InventoryChange change)
        {
            change = default;
            var c = new InventoryChange { Type = (InventoryChangeType)reader.ReadByte() };

            switch (c.Type)
            {
                case InventoryChangeType.SetMaterial:
                    if (!TryReadContainer(reader, out c.Target))
                    {
                        return false;
                    }

                    c.MaterialId = reader.ReadUInt16();
                    c.Count = reader.ReadUInt16();
                    break;

                case InventoryChangeType.AddPotion:
                    if (!TryReadContainer(reader, out c.Target) || !InventoryPacker.TryReadPotion(reader, out c.Potion))
                    {
                        return false;
                    }
                    break;

                case InventoryChangeType.RemovePotion:
                    if (!TryReadContainer(reader, out c.Target))
                    {
                        return false;
                    }

                    c.InstanceId = reader.ReadInt32();
                    break;

                case InventoryChangeType.SetSlot:
                    if (!TryReadContainer(reader, out c.Target) || !InventoryPacker.TryReadSlot(reader, out c.Slot))
                    {
                        return false;
                    }
                    break;

                case InventoryChangeType.CreateCorpse:
                    c.Id = reader.ReadInt32();
                    c.Position = ReadVector(reader);
                    c.Capacity = reader.ReadByte();
                    break;

                case InventoryChangeType.RemoveCorpse:
                case InventoryChangeType.RemovePersonal:
                    c.Id = reader.ReadInt32();
                    break;

                case InventoryChangeType.CreateGround:
                    c.Id = reader.ReadInt32();
                    c.MaterialId = reader.ReadUInt16();
                    c.Count = reader.ReadUInt16();
                    c.Position = ReadVector(reader);
                    break;

                case InventoryChangeType.SetGround:
                    c.Id = reader.ReadInt32();
                    c.Count = reader.ReadUInt16();
                    break;

                default:
                    return false;   // 알 수 없는 종류
            }

            change = c;
            return true;
        }

        // ==================== 공통 ============================

        // 보관 장소 : 종류(byte 1) + 번호(int 4) + 칸(short 2)
        private static void WriteContainer(BinaryWriter writer, ContainerRef container)
        {
            writer.Write((byte)container.Kind);
            writer.Write(container.Id);
            writer.Write(checked((short)container.Slot));
        }

        private static bool TryReadContainer(BinaryReader reader, out ContainerRef container)
        {
            container = default;

            var kind = (ContainerKind)reader.ReadByte();
            int id = reader.ReadInt32();
            int slot = reader.ReadInt16();

            if (kind < ContainerKind.Pack || kind > ContainerKind.Ground)
            {
                return false;
            }

            container = new ContainerRef { Kind = kind, Id = id, Slot = slot };
            return true;
        }

        // 조합 재료 : 출처(보관 장소) + 재료 ID(ushort 2) + 포션 인스턴스 ID(int 4)
        private static void WriteIngredient(BinaryWriter writer, Ingredient ingredient)
        {
            WriteContainer(writer, ingredient.Source);
            writer.Write(checked((ushort)ingredient.MaterialId));
            writer.Write(ingredient.InstanceId);
        }

        private static bool TryReadIngredient(BinaryReader reader, out Ingredient ingredient)
        {
            ingredient = default;

            if (!TryReadContainer(reader, out ContainerRef source))
            {
                return false;
            }

            ingredient = new Ingredient { Source = source, MaterialId = reader.ReadUInt16(), InstanceId = reader.ReadInt32() };
            return true;
        }

        private static void WriteVector(BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static Vector3 ReadVector(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
