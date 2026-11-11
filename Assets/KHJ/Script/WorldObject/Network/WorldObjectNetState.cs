using System;
using System.Collections.Generic;
using System.IO;
using Enzeun.SO;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 씬에 있는 월드 오브젝트들의 "남은 횟수" 사본입니다. 네트워크를 모르는 순수 C# 입니다.
    /// 마스터가 TryValidate 로 검증하고, 전원이 Apply 로 반영하며, 늦게 들어온 사람은 스냅샷으로 맞춥니다. (바닥의 드롭은 스냅샷에 없음)
    /// </summary>
    public sealed class WorldObjectNetState
    {
        private sealed class Entry
        {
            public WorldObjectData Data;
            public int Max;
            public int Remaining;
        }

        private const byte SnapshotVersion = 1;

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<MaterialDropResult> _rollBuffer = new List<MaterialDropResult>();
        private int _version;
        private int _nextDropId = 1;

        /// <summary>
        /// 사본 버전입니다. 적용될 때마다 1 씩 올라갑니다.
        /// </summary>
        public int Version => _version;

        /// <summary>
        /// 등록된 오브젝트 수입니다.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// 남은 횟수가 바뀔 때 (오브젝트 번호, 이전, 이후)로 발생합니다.
        /// </summary>
        public event Action<int, int, int> OnChanged;

        /// <summary>
        /// 소진으로 재료가 떨어질 때 (오브젝트 번호, 드롭 목록, 소진시킨 방식)으로 발생합니다.
        /// 방식이 Interact 이면 상호작용으로 얻은 것이라 공용 인벤으로 바로 들어가고, PotionDamage 이면 바닥 주머니가 됩니다.
        /// </summary>
        public event Action<int, WorldDrop[], WorldObjectHitKind> OnDropped;

        // ==================== 등록 / 조회 ============================

        /// <summary>
        /// 오브젝트를 등록합니다. 번호는 1 이상이고 씬 안에서 고유해야 합니다.
        /// </summary>
        public bool Register(int networkId, WorldObjectData data)
        {
            if (networkId <= 0 || data == null || _entries.ContainsKey(networkId))
            {
                return false;
            }

            _entries.Add(networkId, new Entry { Data = data, Max = data.maxCount, Remaining = data.maxCount });
            return true;
        }

        /// <summary>
        /// 해당 번호의 오브젝트가 등록되어 있는지 확인합니다.
        /// </summary>
        public bool Contains(int networkId)
        {
            return _entries.ContainsKey(networkId);
        }

        /// <summary>
        /// 해당 번호 오브젝트의 남은 횟수를 돌려줍니다.
        /// </summary>
        /// <returns>등록되지 않은 번호이면 -1</returns>
        public int GetRemaining(int networkId)
        {
            return _entries.TryGetValue(networkId, out Entry entry) ? entry.Remaining : -1;
        }

        // ==================== [마스터] 검증 ============================

        /// <summary>
        /// 요청을 검증합니다. 통과하면 마스터가 확정한 적용 메시지를 만들고 true 를 반환하며, 상태는 바꾸지 않습니다.
        /// 거부되면 이유를 돌려주고, 이때는 드롭 번호도 소비하지 않습니다.
        /// </summary>
        public bool TryValidate(WorldObjectCommand request, int requesterActor, out WorldObjectCommand apply, out WorldObjectDenyReason reason)
        {
            apply = default;
            reason = WorldObjectDenyReason.None;

            if (!_entries.TryGetValue(request.NetworkId, out Entry entry))
            {
                reason = WorldObjectDenyReason.NotFound;
                return false;
            }

            if (entry.Remaining <= 0)
            {
                reason = WorldObjectDenyReason.Depleted;
                return false;
            }

            int decrease = WorldObjectRules.ComputeDecrease(entry.Data, request.Kind, request.Damage, request.TargetMask);

            if (decrease <= 0)
            {
                reason = WorldObjectDenyReason.NoEffect;
                return false;
            }

            int remaining = Math.Max(0, entry.Remaining - decrease);

            apply = new WorldObjectCommand
            {
                RequestId = request.RequestId,
                RequesterActor = requesterActor,
                Version = _version + 1,
                NetworkId = request.NetworkId,
                Kind = request.Kind,
                Damage = request.Damage,
                TargetMask = request.TargetMask,
                Previous = entry.Remaining,
                Remaining = remaining,
                Drops = remaining == 0 ? RollDrops(entry.Data) : Array.Empty<WorldDrop>(),
            };

            return true;
        }

        // 소진되었을 때 드롭을 한 번 굴리고 드롭 번호를 붙입니다. (검증을 모두 통과한 뒤에만 호출)
        private WorldDrop[] RollDrops(WorldObjectData data)
        {
            data.RollDrops(_rollBuffer);

            var drops = new List<WorldDrop>(_rollBuffer.Count);

            for (int i = 0; i < _rollBuffer.Count; i++)
            {
                if (_rollBuffer[i].Count <= 0)
                {
                    continue;
                }

                drops.Add(new WorldDrop { DropId = _nextDropId++, MaterialId = _rollBuffer[i].MaterialId, Count = _rollBuffer[i].Count });
            }

            return drops.ToArray();
        }

        // ==================== [전원] 적용 ============================

        /// <summary>
        /// 마스터가 확정한 변경을 사본에 반영합니다.
        /// 이미 반영한 버전이면 무시, 중간 버전을 놓쳤거나 사본이 어긋나 있으면 반영하지 않고 상태만 알려 줍니다.
        /// </summary>
        public WorldObjectApplyStatus Apply(WorldObjectCommand apply, out WorldObjectResult result)
        {
            result = WorldObjectResult.Fail(WorldObjectDenyReason.Invalid);

            if (apply.Version <= _version)
            {
                return WorldObjectApplyStatus.Ignored;
            }

            if (apply.Version != _version + 1)
            {
                return WorldObjectApplyStatus.VersionGap;
            }

            if (!_entries.TryGetValue(apply.NetworkId, out Entry entry)
                || entry.Remaining != apply.Previous
                || apply.Remaining < 0
                || apply.Remaining >= apply.Previous)
            {
                return WorldObjectApplyStatus.Inconsistent;
            }

            entry.Remaining = apply.Remaining;
            _version = apply.Version;

            WorldDrop[] drops = apply.Drops ?? Array.Empty<WorldDrop>();

            // 새 마스터가 같은 번호를 다시 발급하지 않도록 모든 사본이 다음 드롭 번호를 따라갑니다.
            for (int i = 0; i < drops.Length; i++)
            {
                _nextDropId = Math.Max(_nextDropId, drops[i].DropId + 1);
            }

            result = WorldObjectResult.Ok(apply.Remaining, drops);

            OnChanged?.Invoke(apply.NetworkId, apply.Previous, apply.Remaining);

            if (drops.Length > 0)
            {
                OnDropped?.Invoke(apply.NetworkId, drops, apply.Kind);
            }

            return WorldObjectApplyStatus.Applied;
        }

        // ==================== 전체 목록 ============================

        /// <summary>
        /// 전체 목록을 만듭니다. 형식 : 버전(byte), 사본 버전(int), 다음 드롭 번호(int), 개수(ushort), (번호 int, 남은 횟수 ushort) 들.
        /// 기본값(최대 횟수)에서 바뀐 오브젝트만 담습니다.
        /// </summary>
        public byte[] CreateSnapshot()
        {
            var changed = new List<KeyValuePair<int, Entry>>();

            foreach (KeyValuePair<int, Entry> pair in _entries)
            {
                if (pair.Value.Remaining != pair.Value.Max)
                {
                    changed.Add(pair);
                }
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(SnapshotVersion);
                writer.Write(_version);
                writer.Write(_nextDropId);
                writer.Write(checked((ushort)changed.Count));

                for (int i = 0; i < changed.Count; i++)
                {
                    writer.Write(changed[i].Key);
                    writer.Write(checked((ushort)changed[i].Value.Remaining));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 전체 목록으로 사본을 통째로 바꿉니다. 먼저 전부 확인하고, 하나라도 이상하면 아무것도 바꾸지 않고 false 를 반환합니다.
        /// (모르는 오브젝트 번호, 최대 횟수 초과, 번호 중복, 길이 어긋남)
        /// 성공하면 목록에 없는 오브젝트는 최대 횟수로 돌리고, 값이 달라진 오브젝트마다 OnChanged 를 발생시킵니다.
        /// </summary>
        public bool TryLoadSnapshot(byte[] data)
        {
            if (data == null || data.Length < 11 || data[0] != SnapshotVersion)
            {
                return false;
            }

            int version;
            int nextDropId;
            var loaded = new Dictionary<int, int>();

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                reader.ReadByte();
                version = reader.ReadInt32();
                nextDropId = reader.ReadInt32();
                int count = reader.ReadUInt16();

                if (data.Length != 11 + count * 6)
                {
                    return false;
                }

                for (int i = 0; i < count; i++)
                {
                    int networkId = reader.ReadInt32();
                    int remaining = reader.ReadUInt16();

                    if (!_entries.TryGetValue(networkId, out Entry entry) || remaining > entry.Max || loaded.ContainsKey(networkId))
                    {
                        return false;
                    }

                    loaded.Add(networkId, remaining);
                }
            }

            // 여기까지 문제가 없었으니 반영합니다.
            _version = version;
            _nextDropId = Math.Max(_nextDropId, nextDropId);

            var changes = new List<(int id, int previous, int next)>();

            foreach (KeyValuePair<int, Entry> pair in _entries)
            {
                int next = loaded.TryGetValue(pair.Key, out int value) ? value : pair.Value.Max;

                if (pair.Value.Remaining != next)
                {
                    changes.Add((pair.Key, pair.Value.Remaining, next));
                    pair.Value.Remaining = next;
                }
            }

            for (int i = 0; i < changes.Count; i++)
            {
                OnChanged?.Invoke(changes[i].id, changes[i].previous, changes[i].next);
            }

            return true;
        }
    }
}
