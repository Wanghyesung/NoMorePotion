using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 개인 인벤토리입니다. 고정된 칸(기본 10칸)이 있고, 어느 칸이든 재료 묶음이나 포션 한 개를 담을 수 있습니다.
    /// UI 칸과 1:1 로 대응하며, 칸끼리 자유롭게 옮길 수 있습니다. (같은 재료는 합쳐짐, 다른 것은 서로 바뀜)
    /// 죽은 플레이어가 남기는 시체 상자의 내용물도 같은 클래스를 사용합니다.
    /// 팀 공유 인벤토리와 달리 소유자 클라이언트에만 존재하며(죽을 때 TakeAll 로 시체 상자를 만드는 데만 사용),
    /// 네트워크를 모르는 순수 C# 클래스입니다.
    /// </summary>
    public sealed class PersonalInventory
    {
        /// <summary>
        /// 기본 칸 수입니다.
        /// </summary>
        public const int DefaultCapacity = 10;

        private readonly InventorySlot[] _slots;
        private readonly ReadOnlyCollection<InventorySlot> _slotsView;
        private readonly Func<int, int> _limitProvider;

        /// <param name="capacity">칸 수</param>
        /// <param name="limitProvider">재료 ID 별 한 칸의 최대 개수를 알려 주는 함수. 지정하지 않으면 재료 SO 의 최대 보유 개수를 사용</param>
        public PersonalInventory(int capacity = DefaultCapacity, Func<int, int> limitProvider = null)
        {
            _slots = new InventorySlot[Mathf.Max(1, capacity)];
            _slotsView = Array.AsReadOnly(_slots);
            _limitProvider = limitProvider ?? InventoryRules.GetMaterialLimit;
        }

        /// <summary>
        /// 칸 수입니다.
        /// </summary>
        public int Capacity => _slots.Length;

        /// <summary>
        /// 모든 칸입니다. (읽기 전용 · 넣고 빼고 옮기는 것은 이 클래스의 메서드로만)
        /// </summary>
        public IReadOnlyList<InventorySlot> Slots => _slotsView;

        /// <summary>
        /// 칸의 내용이 바뀔 때 그 칸의 위치(index)와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnSlotChanged;

        /// <summary>
        /// 재료를 넣습니다. 같은 재료가 든 칸을 먼저 채우고, 남으면 빈 칸을 씁니다.
        /// </summary>
        /// <returns>넣지 못하고 남은 개수 (0 이면 전부 넣음)</returns>
        public int AddMaterial(int materialId, int count)
        {
            if (materialId <= 0 || count <= 0)
            {
                return Math.Max(0, count);
            }

            int limit = _limitProvider(materialId);
            if (limit <= 0)
            {
                return count;
            }

            int remaining = count;

            // 1) 같은 재료가 든 칸에 먼저 채웁니다.
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].Kind != InventorySlotKind.Material || _slots[i].MaterialId != materialId)
                {
                    continue;
                }

                int room = limit - _slots[i].Count;
                if (room <= 0)
                {
                    continue;
                }

                int moved = Math.Min(room, remaining);
                _slots[i].Count += moved;
                remaining -= moved;
                OnSlotChanged?.Invoke(i);
            }

            // 2) 남으면 빈 칸을 씁니다.
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty)
                {
                    continue;
                }

                int moved = Math.Min(limit, remaining);
                _slots[i] = InventorySlot.OfMaterial(materialId, moved);
                remaining -= moved;
                OnSlotChanged?.Invoke(i);
            }

            return remaining;
        }

        /// <summary>
        /// 포션을 첫 번째 빈 칸에 넣습니다. 마스터를 거쳐 인스턴스 ID 를 받은 포션만 받습니다.
        /// </summary>
        /// <returns>들어간 칸의 위치. 빈 칸이 없거나, null 이거나, ID 가 없으면 -1 (ID 가 없는 포션은 받지 않고 버려집니다)</returns>
        public int AddPotion(PotionInstance potion)
        {
            if (potion == null)
            {
                return -1;
            }

            // 포션 ID 는 마스터만 발급합니다. 로컬에서 만든 채 ID 가 없는 포션을 받으면 ID 가 겹치거나 네트워크로 묶지 못해 시체 상자가 깨집니다.
            if (potion.InstanceId <= 0)
            {
                Debug.LogWarning("[PersonalInventory]: 인스턴스 ID 가 없는 포션은 넣을 수 없어 버립니다. 마스터를 거쳐 ID 를 받은 포션만 넣어 주세요.");
                return -1;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    _slots[i] = InventorySlot.OfPotion(potion);
                    OnSlotChanged?.Invoke(i);
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 칸 하나를 다른 칸으로 옮깁니다. (UI 에서 끌어다 놓기)
        /// 도착 칸이 비어 있으면 이동, 같은 재료면 한도까지 합치고 남은 것은 원래 칸에 두며, 그 외에는 서로 바꿉니다.
        /// </summary>
        /// <returns>무언가 바뀌었으면 true</returns>
        public bool Move(int from, int to)
        {
            if (!IsValidIndex(from) || !IsValidIndex(to) || from == to || _slots[from].IsEmpty)
            {
                return false;
            }

            // 도착 칸이 비어 있으면 그대로 이동
            if (_slots[to].IsEmpty)
            {
                _slots[to] = _slots[from];
                _slots[from] = default;
                OnSlotChanged?.Invoke(from);
                OnSlotChanged?.Invoke(to);
                return true;
            }

            // 같은 재료끼리는 합칩니다.
            if (_slots[from].Kind == InventorySlotKind.Material
                && _slots[to].Kind == InventorySlotKind.Material
                && _slots[from].MaterialId == _slots[to].MaterialId)
            {
                int room = _limitProvider(_slots[to].MaterialId) - _slots[to].Count;
                int moved = Math.Min(Math.Max(0, room), _slots[from].Count);

                // 도착 칸이 이미 가득 찼으면 합칠 수 없고, 같은 재료끼리 바꾸는 것도 의미가 없습니다.
                if (moved <= 0)
                {
                    return false;
                }

                _slots[to].Count += moved;
                _slots[from].Count -= moved;

                if (_slots[from].Count <= 0)
                {
                    _slots[from] = default;
                }

                OnSlotChanged?.Invoke(from);
                OnSlotChanged?.Invoke(to);
                return true;
            }

            // 그 외에는 서로 바꿉니다.
            InventorySlot temp = _slots[to];
            _slots[to] = _slots[from];
            _slots[from] = temp;
            OnSlotChanged?.Invoke(from);
            OnSlotChanged?.Invoke(to);
            return true;
        }

        /// <summary>
        /// 다른 곳(시체 상자 등)에서 가져온 내용을 지정한 칸에 놓습니다.
        /// 빈 칸이면 그대로 놓고, 같은 재료가 든 칸이면 한도까지 합치며, 그 외에는 놓지 못합니다. (바꾸지 않음)
        /// </summary>
        /// <param name="leftover">놓지 못하고 남은 내용. 전부 놓았으면 빈 칸</param>
        /// <returns>하나라도 놓았으면 true</returns>
        public bool TryPlace(int index, InventorySlot content, out InventorySlot leftover)
        {
            if (!PreviewPlace(index, content, out InventorySlot placed, out leftover))
            {
                return false;
            }

            if (_slots[index].IsEmpty)
            {
                _slots[index] = placed;
            }
            else
            {
                _slots[index].Count += placed.Count;
            }

            OnSlotChanged?.Invoke(index);
            return true;
        }

        /// <summary>
        /// TryPlace 를 실제로 하지 않고 결과만 계산합니다. (칸을 바꾸지 않음)
        /// 멀티플레이에서 마스터가 "얼마나 들어가는가"를 검증한 뒤, 그 결과를 모든 클라이언트가 똑같이 적용하는 데 사용합니다.
        /// </summary>
        /// <param name="placed">실제로 놓이는 내용</param>
        /// <param name="leftover">놓지 못하고 남는 내용</param>
        /// <returns>하나라도 놓을 수 있으면 true</returns>
        public bool PreviewPlace(int index, InventorySlot content, out InventorySlot placed, out InventorySlot leftover)
        {
            placed = default;
            leftover = content;

            if (!IsValidIndex(index) || content.IsEmpty)
            {
                return false;
            }

            if (_slots[index].IsEmpty)
            {
                // 빈 칸이어도 재료는 한 칸의 한도를 넘겨 놓을 수 없습니다. (한도를 넘는 묶음이 시체 상자 검증을 통과하는 것을 막음)
                if (content.Kind == InventorySlotKind.Material)
                {
                    int limit = _limitProvider(content.MaterialId);

                    if (limit <= 0 || content.Count <= 0)
                    {
                        return false;
                    }

                    int placedCount = Math.Min(limit, content.Count);
                    placed = InventorySlot.OfMaterial(content.MaterialId, placedCount);
                    leftover = content.Count - placedCount > 0 ? InventorySlot.OfMaterial(content.MaterialId, content.Count - placedCount) : default;
                    return true;
                }

                placed = content;
                leftover = default;
                return true;
            }

            if (content.Kind == InventorySlotKind.Material
                && _slots[index].Kind == InventorySlotKind.Material
                && _slots[index].MaterialId == content.MaterialId)
            {
                int room = _limitProvider(content.MaterialId) - _slots[index].Count;
                int moved = Math.Min(Math.Max(0, room), content.Count);

                if (moved <= 0)
                {
                    return false;
                }

                placed = InventorySlot.OfMaterial(content.MaterialId, moved);
                leftover = content.Count - moved > 0 ? InventorySlot.OfMaterial(content.MaterialId, content.Count - moved) : default;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 칸의 내용을 통째로 꺼내고 그 칸을 비웁니다. 위치가 잘못되었거나 빈 칸이면 빈 칸을 반환합니다.
        /// </summary>
        public InventorySlot TakeSlot(int index)
        {
            if (!IsValidIndex(index) || _slots[index].IsEmpty)
            {
                return default;
            }

            InventorySlot taken = _slots[index];
            _slots[index] = default;
            OnSlotChanged?.Invoke(index);
            return taken;
        }

        /// <summary>
        /// 모든 칸의 내용을 칸 위치 그대로 꺼내고 전부 비웁니다. 죽을 때 시체 상자를 만드는 데 사용합니다.
        /// 먼저 비워서 돌려주므로, 이 결과를 쓰는 동안 같은 아이템이 두 군데 존재하지 않습니다. (복제 방지)
        /// </summary>
        public InventorySlot[] TakeAll()
        {
            var all = new InventorySlot[_slots.Length];
            Array.Copy(_slots, all, _slots.Length);

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    continue;
                }

                _slots[i] = default;
                OnSlotChanged?.Invoke(i);
            }

            return all;
        }

        /// <summary>
        /// 칸 배열을 그대로 불러옵니다. 시체 상자를 만들 때 TakeAll 결과나 네트워크로 받은 내용을 적용하는 데 사용합니다.
        /// 기존 내용은 지워지고, 칸 수보다 많은 입력은 무시합니다. OnSlotChanged 를 모든 칸에 발생시킵니다.
        /// </summary>
        public void Load(IReadOnlyList<InventorySlot> source)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = source != null && i < source.Count ? source[i] : default;
                OnSlotChanged?.Invoke(i);
            }
        }

        /// <summary>
        /// 해당 재료의 전체 개수입니다. (여러 칸에 나뉘어 있으면 합산)
        /// </summary>
        public int GetMaterialCount(int materialId)
        {
            int total = 0;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Kind == InventorySlotKind.Material && _slots[i].MaterialId == materialId)
                {
                    total += _slots[i].Count;
                }
            }

            return total;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < _slots.Length;
        }
    }
}
