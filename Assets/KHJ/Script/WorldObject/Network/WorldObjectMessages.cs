using System;
using System.IO;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트 네트워크 메시지(요청, 적용, 거부)를 byte 배열로 묶고 푸는 정적 도우미입니다. 순수 C# 이라 에디터에서 왕복 시험을 합니다.
    ///
    /// 형식 (리틀 엔디언, 맨 앞은 항상 버전 1 byte)
    /// - 요청 : 버전, 요청 번호(int), 종류(byte), 오브젝트 번호(int), 대미지(ushort), 대상 종류 마스크(byte)
    /// - 적용 : 버전, 요청 번호(int), 요청한 사람(int), 사본 버전(int), 오브젝트 번호(int), 타격 종류(byte), 이전 횟수(ushort), 남은 횟수(ushort), 드롭 수(byte), 드롭들(번호 int, 재료 ushort, 개수 ushort)
    ///   (타격 종류는 상호작용으로 얻은 재료가 공용 인벤으로 바로 들어갈지, 바닥 주머니가 될지 가르는 데 쓰므로 모든 사본이 같은 값을 받아야 합니다)
    /// - 거부 : 버전, 요청 번호(int), 이유(byte)
    /// 형식이 어긋나면(모자람, 남음, 모르는 값) 예외 없이 false 를 반환합니다.
    /// </summary>
    public static class WorldObjectMessages
    {
        // 버전과 맞지않는 이전 형식의 메시지는 받지 않음
        public const byte Version = 1;

        private const int RequestLength = 13;
        private const int DenyLength = 6;
        private const int ApplyHeaderLength = 1 + 4 + 4 + 4 + 4 + 1 + 2 + 2 + 1;
        private const int DropLength = 4 + 2 + 2;

        // ==================== 요청 (클라이언트 -> 마스터) ============================

        /// <summary>
        /// 요청을 byte 배열로 묶습니다. 범위 초과 시 OverflowException.
        /// (대미지는 ushort, 대상 종류 마스크는 byte 범위)
        /// </summary>
        public static byte[] PackRequest(WorldObjectCommand command)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(command.RequestId);
                writer.Write((byte)command.Kind);
                writer.Write(command.NetworkId);
                writer.Write(checked((ushort)command.Damage));
                writer.Write(checked((byte)command.TargetMask));

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 요청을 풉니다. 길이, 버전, 종류가 맞지 않으면 false 를 반환합니다.
        /// </summary>
        public static bool TryUnpackRequest(byte[] data, out WorldObjectCommand command)
        {
            command = default;

            if (data == null || data.Length != RequestLength || data[0] != Version)
            {
                return false;
            }

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                reader.ReadByte();

                var c = new WorldObjectCommand
                {
                    RequestId = reader.ReadInt32(),
                    Kind = (WorldObjectHitKind)reader.ReadByte(),
                    NetworkId = reader.ReadInt32(),
                    Damage = reader.ReadUInt16(),
                    TargetMask = reader.ReadByte(),
                };

                if (c.Kind != WorldObjectHitKind.Interact && c.Kind != WorldObjectHitKind.PotionDamage)
                {
                    return false;
                }

                command = c;
                return true;
            }
        }

        /// <summary>
        /// 요청이 깨졌더라도 요청 번호만 읽을 수 있으면 읽습니다. 마스터가 바로 거부해서 요청한 쪽이 제한 시간까지 기다리지 않게 합니다.
        /// </summary>
        public static bool TryPeekRequestId(byte[] data, out int requestId)
        {
            requestId = 0;

            if (data == null || data.Length < 5 || data[0] != Version)
            {
                return false;
            }

            requestId = BitConverter.ToInt32(data, 1);
            return true;
        }

        // ==================== 적용 (마스터 -> 전원) ============================

        /// <summary>
        /// 마스터가 확정한 적용 메시지를 byte 배열로 묶습니다. 값이 ushort/byte 범위를 넘으면 OverflowException.
        /// </summary>
        public static byte[] PackApply(WorldObjectCommand command)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(command.RequestId);
                writer.Write(command.RequesterActor);
                writer.Write(command.Version);
                writer.Write(command.NetworkId);
                writer.Write((byte)command.Kind);
                writer.Write(checked((ushort)command.Previous));
                writer.Write(checked((ushort)command.Remaining));

                WorldDrop[] drops = command.Drops;
                writer.Write(checked((byte)(drops != null ? drops.Length : 0)));

                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        writer.Write(drops[i].DropId);
                        writer.Write(checked((ushort)drops[i].MaterialId));
                        writer.Write(checked((ushort)drops[i].Count));
                    }
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 적용 메시지를 풉니다. 길이(드롭 수 포함)나 버전이 맞지 않으면 false 를 반환합니다.
        /// </summary>
        public static bool TryUnpackApply(byte[] data, out WorldObjectCommand command)
        {
            command = default;

            if (data == null || data.Length < ApplyHeaderLength || data[0] != Version)
            {
                return false;
            }

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                reader.ReadByte();

                var c = new WorldObjectCommand
                {
                    RequestId = reader.ReadInt32(),
                    RequesterActor = reader.ReadInt32(),
                    Version = reader.ReadInt32(),
                    NetworkId = reader.ReadInt32(),
                };

                c.Kind = (WorldObjectHitKind)reader.ReadByte();

                // 요청과 같은 두 종류만 있습니다. 그 밖의 값은 깨진 메시지입니다.
                if (c.Kind != WorldObjectHitKind.Interact && c.Kind != WorldObjectHitKind.PotionDamage)
                {
                    return false;
                }

                c.Previous = reader.ReadUInt16();
                c.Remaining = reader.ReadUInt16();

                int dropCount = reader.ReadByte();

                // 길이가 정확히 맞아야 합니다. (모자라거나 남으면 깨진 메시지)
                if (data.Length != ApplyHeaderLength + dropCount * DropLength)
                {
                    return false;
                }

                var drops = new WorldDrop[dropCount];

                for (int i = 0; i < dropCount; i++)
                {
                    drops[i] = new WorldDrop
                    {
                        DropId = reader.ReadInt32(),
                        MaterialId = reader.ReadUInt16(),
                        Count = reader.ReadUInt16(),
                    };
                }

                c.Drops = drops;
                command = c;
                return true;
            }
        }

        // ==================== 거부 (마스터 -> 요청한 사람) ============================

        /// <summary>
        /// 거부 메시지(요청 번호, 이유)를 byte 배열로 묶습니다.
        /// </summary>
        public static byte[] PackDeny(int requestId, WorldObjectDenyReason reason)
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
        /// 거부 메시지를 풉니다. 길이나 버전이 맞지 않으면 false 를 반환합니다.
        /// </summary>
        public static bool TryUnpackDeny(byte[] data, out int requestId, out WorldObjectDenyReason reason)
        {
            requestId = 0;
            reason = WorldObjectDenyReason.None;

            if (data == null || data.Length != DenyLength || data[0] != Version)
            {
                return false;
            }

            requestId = BitConverter.ToInt32(data, 1);
            reason = (WorldObjectDenyReason)data[5];
            return true;
        }
    }
}
