using System;
using System.Collections.Generic;
using System.IO;
using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 내용을 ID 만 담은 작은 byte 배열로 묶고(Pack) 다시 푸는(TryUnpack) 정적 도우미입니다.
    /// SO 는 네트워크로 보낼 수 없으므로 모든 아이템을 ID 로 바꿔 보내고, 받는 쪽이 ID 로 SO 를 다시 찾습니다.
    ///
    /// 형식 (모든 값은 리틀 엔디언)
    /// - 모든 묶음의 맨 앞은 버전 1 byte 입니다. 버전이 다르면 풀지 않습니다.
    /// - 포션 한 개 : 인스턴스 ID(int 4) + 포션 ID(ushort 2) + 함정 키워드 ID(ushort 2, 없으면 0) = 8 byte
    /// - 재료 : 재료 ID(ushort 2) + 개수(ushort 2) = 4 byte
    /// - 칸 : 종류(byte 1) + [재료면 재료 ID, 개수 / 포션이면 포션 한 개]
    ///
    /// 풀기 실패는 예외 대신 false 를 반환합니다. (모자라거나 남는 byte, 알 수 없는 ID, 다른 버전 모두 실패)
    /// 쓰기와 읽기의 순서가 어긋나지 않도록, 각 항목을 쓰는 코드와 읽는 코드를 바로 위아래에 붙여 두었습니다.
    /// </summary>
    public static class InventoryPacker
    {
        /// <summary>
        /// 현재 형식의 버전입니다. 형식을 바꾸면 올립니다.
        /// </summary>
        public const byte Version = 1;

        // ==================== 포션 목록 ============================

        public static byte[] PackPotions(IReadOnlyList<PotionInstance> potions)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(checked((ushort)potions.Count));

                for (int i = 0; i < potions.Count; i++)
                {
                    WritePotion(writer, potions[i]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryUnpackPotions(byte[] data, out List<PotionInstance> potions)
        {
            List<PotionInstance> result = null;

            bool success = TryRead(data, reader =>
            {
                int count = reader.ReadUInt16();
                var list = new List<PotionInstance>(count);

                for (int i = 0; i < count; i++)
                {
                    if (!TryReadPotion(reader, out PotionInstance potion))
                    {
                        return false;
                    }

                    list.Add(potion);
                }

                result = list;
                return true;
            });

            potions = success ? result : null;
            return success;
        }

        // ==================== 재료 ============================

        public static byte[] PackMaterials(IReadOnlyDictionary<int, int> counts)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(checked((ushort)counts.Count));

                foreach (KeyValuePair<int, int> entry in counts)
                {
                    writer.Write(checked((ushort)entry.Key));
                    writer.Write(checked((ushort)entry.Value));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryUnpackMaterials(byte[] data, out Dictionary<int, int> counts)
        {
            Dictionary<int, int> result = null;

            bool success = TryRead(data, reader =>
            {
                int count = reader.ReadUInt16();
                var map = new Dictionary<int, int>(count);

                for (int i = 0; i < count; i++)
                {
                    int materialId = reader.ReadUInt16();
                    int amount = reader.ReadUInt16();

                    if (!IsKnownMaterial(materialId) || amount <= 0 || map.ContainsKey(materialId))
                    {
                        return false;
                    }

                    map.Add(materialId, amount);
                }

                result = map;
                return true;
            });

            counts = success ? result : null;
            return success;
        }

        // ==================== 칸 (개인 인벤토리, 시체 상자) ============================

        public static byte[] PackSlots(IReadOnlyList<InventorySlot> slots)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(checked((byte)slots.Count));

                for (int i = 0; i < slots.Count; i++)
                {
                    WriteSlot(writer, slots[i]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static bool TryUnpackSlots(byte[] data, out InventorySlot[] slots)
        {
            InventorySlot[] unpacked = null;

            bool success = TryRead(data, reader =>
            {
                int count = reader.ReadByte();
                var result = new InventorySlot[count];

                for (int i = 0; i < count; i++)
                {
                    if (!TryReadSlot(reader, out InventorySlot slot))
                    {
                        return false;
                    }

                    result[i] = slot;
                }

                unpacked = result;
                return true;
            });

            slots = success ? unpacked : null;
            return success;
        }

        // ==================== 칸 한 개 (네트워크 메시지에서도 사용) ============================

        /// <summary>
        /// 칸 한 개를 씁니다. 종류(byte 1) + [재료면 재료 ID, 개수 / 포션이면 포션 한 개]
        /// </summary>
        public static void WriteSlot(BinaryWriter writer, InventorySlot slot)
        {
            writer.Write((byte)slot.Kind);

            switch (slot.Kind)
            {
                case InventorySlotKind.Material:
                    writer.Write(checked((ushort)slot.MaterialId));
                    writer.Write(checked((ushort)slot.Count));
                    break;

                case InventorySlotKind.Potion:
                    WritePotion(writer, slot.Potion);
                    break;
            }
        }

        /// <summary>
        /// 칸 한 개를 읽습니다. 알 수 없는 종류, 알 수 없는 ID, 개수 0 이하이면 false 입니다.
        /// </summary>
        public static bool TryReadSlot(BinaryReader reader, out InventorySlot slot)
        {
            slot = default;

            var kind = (InventorySlotKind)reader.ReadByte();

            switch (kind)
            {
                case InventorySlotKind.Empty:
                    return true;

                case InventorySlotKind.Material:
                    int materialId = reader.ReadUInt16();
                    int amount = reader.ReadUInt16();

                    if (!IsKnownMaterial(materialId) || amount <= 0)
                    {
                        return false;
                    }

                    slot = InventorySlot.OfMaterial(materialId, amount);
                    return true;

                case InventorySlotKind.Potion:
                    if (!TryReadPotion(reader, out PotionInstance potion))
                    {
                        return false;
                    }

                    slot = InventorySlot.OfPotion(potion);
                    return true;

                default:
                    return false;   // 알 수 없는 종류
            }
        }

        // ==================== 포션 한 개 (목록, 칸, 네트워크 메시지가 함께 사용) ============================

        /// <summary>
        /// 포션 한 개를 씁니다. 인스턴스 ID(int 4) + 포션 ID(ushort 2) + 함정 키워드 ID(ushort 2, 없으면 0)
        /// </summary>
        public static void WritePotion(BinaryWriter writer, PotionInstance potion)
        {
            writer.Write(potion.InstanceId);
            writer.Write(checked((ushort)potion.Data.potionID));
            writer.Write(checked((ushort)(potion.TrapKeyword != null ? potion.TrapKeyword.KeywordID : 0)));
        }

        /// <summary>
        /// 포션 한 개를 읽습니다. 인스턴스 ID 가 0 이하이거나 알 수 없는 포션/키워드 ID 이면 false 입니다.
        /// </summary>
        public static bool TryReadPotion(BinaryReader reader, out PotionInstance potion)
        {
            potion = null;

            int instanceId = reader.ReadInt32();
            int potionId = reader.ReadUInt16();
            int keywordId = reader.ReadUInt16();

            PotionDataManager manager = PotionDataManager.Instance;
            if (manager == null || instanceId <= 0 || !manager.TryGetPotionData(potionId, out PotionData data))
            {
                return false;
            }

            TrapPotionKeyword keyword = null;
            if (keywordId != 0 && !manager.TryGetTrapKeyword(keywordId, out keyword))
            {
                return false;
            }

            potion = new PotionInstance(data, keyword, instanceId);
            return true;
        }

        // ==================== 공통 ============================

        private static bool IsKnownMaterial(int materialId)
        {
            return PotionDataManager.Instance != null
                && PotionDataManager.Instance.TryGetMaterialData(materialId, out MaterialData _);
        }

        /// <summary>
        /// 버전을 확인하고, 읽기 함수를 실행한 뒤, 남는 byte 가 없는지까지 확인합니다.
        /// 어디서든 형식이 어긋나면 예외 대신 false 를 반환합니다.
        /// </summary>
        private static bool TryRead(byte[] data, Func<BinaryReader, bool> readBody)
        {
            if (data == null || data.Length < 1)
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
                        Debug.LogWarning("[InventoryPacker]: 지원하지 않는 버전의 데이터입니다.");
                        return false;
                    }

                    if (!readBody(reader))
                    {
                        return false;
                    }

                    // 다 읽었는데 byte 가 남으면 쓰기와 읽기의 형식이 어긋난 것입니다.
                    return stream.Position == stream.Length;
                }
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }
    }
}
