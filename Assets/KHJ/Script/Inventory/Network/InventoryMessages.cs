using System.IO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 네트워크 메시지(요청, 적용, 거부)를 byte 배열로 묶고 푸는 정적 도우미입니다.
    /// 네트워크 라이브러리를 모르는 순수 C# 이라서 에디터에서 왕복 시험을 할 수 있습니다.
    ///
    /// 형식 (리틀 엔디언, 맨 앞은 항상 버전 1 byte)
    /// - 요청  : 버전, 종류, 요청 번호(int), 종류별 내용
    /// - 적용  : 버전, 종류, 요청 번호(int), 요청한 사람(int), 사본 버전(int), 종류별 내용
    /// - 거부  : 버전, 요청 번호(int), 이유(byte)
    /// 형식이 어긋나면(모자람, 남음, 모르는 값) 예외 없이 false 를 반환합니다.
    /// 종류별 내용은 쓰는 코드와 읽는 코드를 바로 위아래에 붙여 순서가 어긋나지 않게 했습니다.
    /// </summary>
    public static class InventoryMessages
    {
        /// <summary>
        /// 현재 형식의 버전입니다. 형식을 바꾸면 올립니다.
        /// </summary>
        public const byte Version = 1;

        // ==================== 요청 (클라이언트 -> 마스터) ============================

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
                    case InventoryOp.AddMaterial:
                    case InventoryOp.RemoveMaterial:
                        writer.Write(checked((ushort)command.MaterialId));
                        writer.Write(checked((ushort)command.Count));
                        break;

                    case InventoryOp.AddPotion:
                        writer.Write(checked((ushort)command.PotionId));
                        writer.Write(checked((ushort)command.KeywordId));
                        break;

                    case InventoryOp.TakePotion:
                        writer.Write(command.InstanceId);
                        break;

                    case InventoryOp.CreateCorpse:
                        WriteVector(writer, command.Position);
                        WriteSlots(writer, command.Slots);
                        break;

                    case InventoryOp.TakeCorpseSlot:
                        writer.Write(command.CorpseId);
                        writer.Write(checked((byte)command.SlotIndex));
                        break;

                    case InventoryOp.PutCorpseSlot:
                        writer.Write(command.CorpseId);
                        writer.Write(checked((byte)command.SlotIndex));
                        InventoryPacker.WriteSlot(writer, command.Slot);
                        break;
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

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
                        case InventoryOp.AddMaterial:
                        case InventoryOp.RemoveMaterial:
                            c.MaterialId = reader.ReadUInt16();
                            c.Count = reader.ReadUInt16();
                            break;

                        case InventoryOp.AddPotion:
                            c.PotionId = reader.ReadUInt16();
                            c.KeywordId = reader.ReadUInt16();
                            break;

                        case InventoryOp.TakePotion:
                            c.InstanceId = reader.ReadInt32();
                            break;

                        case InventoryOp.CreateCorpse:
                            c.Position = ReadVector(reader);
                            if (!TryReadSlots(reader, out c.Slots))
                            {
                                return false;
                            }
                            break;

                        case InventoryOp.TakeCorpseSlot:
                            c.CorpseId = reader.ReadInt32();
                            c.SlotIndex = reader.ReadByte();
                            break;

                        case InventoryOp.PutCorpseSlot:
                            c.CorpseId = reader.ReadInt32();
                            c.SlotIndex = reader.ReadByte();
                            if (!InventoryPacker.TryReadSlot(reader, out c.Slot))
                            {
                                return false;
                            }
                            break;

                        default:
                            return false;   // 알 수 없는 종류
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

                switch (command.Op)
                {
                    case InventoryOp.AddMaterial:
                    case InventoryOp.RemoveMaterial:
                        writer.Write(checked((ushort)command.MaterialId));
                        writer.Write(checked((ushort)command.NewTotal));
                        writer.Write(checked((ushort)command.Count));
                        break;

                    case InventoryOp.AddPotion:
                        InventoryPacker.WritePotion(writer, command.Potion);
                        break;

                    case InventoryOp.TakePotion:
                        writer.Write(command.InstanceId);
                        break;

                    case InventoryOp.CreateCorpse:
                        writer.Write(command.CorpseId);
                        WriteVector(writer, command.Position);
                        WriteSlots(writer, command.Slots);
                        break;

                    case InventoryOp.TakeCorpseSlot:
                        writer.Write(command.CorpseId);
                        writer.Write(checked((byte)command.SlotIndex));
                        break;

                    case InventoryOp.PutCorpseSlot:
                        writer.Write(command.CorpseId);
                        writer.Write(checked((byte)command.SlotIndex));
                        InventoryPacker.WriteSlot(writer, command.Slot);
                        InventoryPacker.WriteSlot(writer, command.Leftover);
                        break;
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryUnpackApply(byte[] data, out InventoryCommand command)
        {
            command = default;

            if (data == null || data.Length < 14)
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
                    };

                    switch (c.Op)
                    {
                        case InventoryOp.AddMaterial:
                        case InventoryOp.RemoveMaterial:
                            c.MaterialId = reader.ReadUInt16();
                            c.NewTotal = reader.ReadUInt16();
                            c.Count = reader.ReadUInt16();
                            break;

                        case InventoryOp.AddPotion:
                            if (!InventoryPacker.TryReadPotion(reader, out c.Potion))
                            {
                                return false;
                            }

                            c.InstanceId = c.Potion.InstanceId;
                            c.PotionId = c.Potion.Data.potionID;
                            c.KeywordId = c.Potion.TrapKeyword != null ? c.Potion.TrapKeyword.KeywordID : 0;
                            break;

                        case InventoryOp.TakePotion:
                            c.InstanceId = reader.ReadInt32();
                            break;

                        case InventoryOp.CreateCorpse:
                            c.CorpseId = reader.ReadInt32();
                            c.Position = ReadVector(reader);
                            if (!TryReadSlots(reader, out c.Slots))
                            {
                                return false;
                            }
                            break;

                        case InventoryOp.TakeCorpseSlot:
                            c.CorpseId = reader.ReadInt32();
                            c.SlotIndex = reader.ReadByte();
                            break;

                        case InventoryOp.PutCorpseSlot:
                            c.CorpseId = reader.ReadInt32();
                            c.SlotIndex = reader.ReadByte();
                            if (!InventoryPacker.TryReadSlot(reader, out c.Slot) || !InventoryPacker.TryReadSlot(reader, out c.Leftover))
                            {
                                return false;
                            }
                            break;

                        default:
                            return false;
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

        // ==================== 공통 ============================

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

        // 칸 목록 : 칸 수(byte 1) + 칸들. (묶음마다 버전을 붙이지 않는 내부용)
        private static void WriteSlots(BinaryWriter writer, InventorySlot[] slots)
        {
            // null 은 빈 목록으로 보냅니다. (마스터가 "잘못된 요청"으로 거부하게 함)
            if (slots == null)
            {
                writer.Write((byte)0);
                return;
            }

            writer.Write(checked((byte)slots.Length));

            for (int i = 0; i < slots.Length; i++)
            {
                InventoryPacker.WriteSlot(writer, slots[i]);
            }
        }

        private static bool TryReadSlots(BinaryReader reader, out InventorySlot[] slots)
        {
            slots = null;

            int count = reader.ReadByte();
            var result = new InventorySlot[count];

            for (int i = 0; i < count; i++)
            {
                if (!InventoryPacker.TryReadSlot(reader, out result[i]))
                {
                    return false;
                }
            }

            slots = result;
            return true;
        }
    }
}
