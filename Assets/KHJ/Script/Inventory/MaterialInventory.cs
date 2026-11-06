using System;
using System.Collections.Generic;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 재료를 담는 팀 공유 인벤토리입니다. 재료 ID 별 개수로 보관합니다. (칸 개념 없음 · 종류별 한도만 있음)
    /// 네트워크를 모르는 순수 C# 클래스이며, 멀티플레이에서는 권한을 가진 쪽이 검증한 결과를 각 클라이언트가 이 클래스에 적용합니다.
    /// </summary>
    public sealed class MaterialInventory
    {
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
        private readonly Func<int, int> _limitProvider;

        /// <param name="limitProvider">재료 ID 별 보관 한도를 알려 주는 함수. 지정하지 않으면 재료 SO 의 최대 보유 개수를 사용</param>
        public MaterialInventory(Func<int, int> limitProvider = null)
        {
            _limitProvider = limitProvider ?? InventoryRules.GetMaterialLimit;
        }

        /// <summary>
        /// 재료 ID 별 보관 개수입니다. (읽기 전용 · 넣고 빼는 것은 Add / Remove 로만)
        /// </summary>
        public IReadOnlyDictionary<int, int> Counts => _counts;

        /// <summary>
        /// 재료의 보관 개수가 바뀔 때 해당 재료 ID 와 함께 발생합니다.
        /// </summary>
        public event Action<int> OnChanged;

        /// <summary>
        /// 해당 재료의 보관 개수입니다. 없으면 0 입니다.
        /// </summary>
        public int GetCount(int materialId)
        {
            return _counts.TryGetValue(materialId, out int count) ? count : 0;
        }

        /// <summary>
        /// 해당 재료를 더 넣을 수 있는 개수입니다. (한도 - 현재 개수, 음수가 되지 않음)
        /// 멀티플레이에서 마스터가 넣기 전에 얼마나 들어가는지 검증하는 데 사용합니다.
        /// </summary>
        public int GetRoom(int materialId)
        {
            return Math.Max(0, _limitProvider(materialId) - GetCount(materialId));
        }

        /// <summary>
        /// 재료를 넣습니다. 한도를 넘는 만큼은 넣지 않습니다.
        /// </summary>
        /// <returns>실제로 넣은 개수 (0 이면 하나도 넣지 못함)</returns>
        public int Add(int materialId, int count)
        {
            if (materialId <= 0 || count <= 0)
            {
                return 0;
            }

            int current = GetCount(materialId);
            int room = _limitProvider(materialId) - current;
            int accepted = Math.Min(count, Math.Max(0, room));

            if (accepted <= 0)
            {
                return 0;
            }

            _counts[materialId] = current + accepted;
            OnChanged?.Invoke(materialId);
            return accepted;
        }

        /// <summary>
        /// 재료를 뺍니다. 개수가 모자라면 아무것도 빼지 않고 false 를 반환합니다. (일부만 빼지 않음)
        /// </summary>
        public bool Remove(int materialId, int count)
        {
            if (materialId <= 0 || count <= 0)
            {
                return false;
            }

            int current = GetCount(materialId);
            if (current < count)
            {
                return false;
            }

            if (current == count)
            {
                _counts.Remove(materialId);
            }
            else
            {
                _counts[materialId] = current - count;
            }

            OnChanged?.Invoke(materialId);
            return true;
        }

        /// <summary>
        /// 해당 재료의 개수를 지정한 값으로 정합니다. 마스터가 검증을 끝낸 결과나 전체 목록(스냅샷)을 적용할 때 사용합니다.
        /// 한도 검사는 하지 않습니다. (권한을 가진 쪽이 이미 검증함)
        /// </summary>
        public void SetCount(int materialId, int count)
        {
            if (materialId <= 0)
            {
                return;
            }

            int before = GetCount(materialId);
            int after = Math.Max(0, count);

            if (before == after)
            {
                return;
            }

            if (after == 0)
            {
                _counts.Remove(materialId);
            }
            else
            {
                _counts[materialId] = after;
            }

            OnChanged?.Invoke(materialId);
        }

        /// <summary>
        /// 모든 재료를 비웁니다. 전체 목록을 새로 받아 적용하기 전에 사용합니다.
        /// </summary>
        public void Clear()
        {
            if (_counts.Count == 0)
            {
                return;
            }

            var ids = new List<int>(_counts.Keys);
            _counts.Clear();

            for (int i = 0; i < ids.Count; i++)
            {
                OnChanged?.Invoke(ids[i]);
            }
        }
    }
}
