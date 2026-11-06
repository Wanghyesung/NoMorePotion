using System;
using System.Collections.Generic;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 뱃속(인벤토리)에 보관된 포션 인스턴스 목록입니다. 제작한 포션을 한 개씩 PotionInstance 로 보관합니다.
    /// 함정 여부가 포션마다 다르므로 같은 포션이어도 스택하지 않고 개별로 보관합니다.
    /// </summary>
    public sealed class PotionInventory
    {
        /// <summary>
        /// 기본 보관 한도입니다.
        /// </summary>
        public const int DefaultCapacity = 100;

        private readonly List<PotionInstance> _items = new List<PotionInstance>();

        public PotionInventory(int capacity = DefaultCapacity)
        {
            Capacity = Mathf.Max(1, capacity);
        }

        /// <summary>
        /// 보관할 수 있는 최대 개수입니다.
        /// </summary>
        public int Capacity { get; }

        public int Count => _items.Count;

        public bool IsFull => _items.Count >= Capacity;

        /// <summary>
        /// 보관 중인 포션 목록입니다. (읽기 전용 · 넣고 빼는 것은 Add / TakeAt / TakeFirst 로만)
        /// </summary>
        public IReadOnlyList<PotionInstance> Items => _items;

        /// <summary>
        /// 포션이 들어오거나 빠져서 목록이 바뀔 때 발생합니다.
        /// </summary>
        public event Action OnChanged;

        /// <summary>
        /// 포션을 넣습니다. 가득 찼거나 null 이면 넣지 않고 false 를 반환합니다.
        /// </summary>
        public bool Add(PotionInstance potion)
        {
            if (potion == null)
            {
                Debug.LogWarning("[PotionInventory]: null 포션은 넣을 수 없습니다.");
                return false;
            }

            if (IsFull)
            {
                Debug.LogWarning($"[PotionInventory]: 뱃속이 가득 찼습니다. ({Count}/{Capacity})");
                return false;
            }

            _items.Add(potion);

#if UNITY_EDITOR
            Debug.Log($"[PotionInventory] : {potion.Data.potionName} 보관 ({Count}/{Capacity})");
#endif

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// index 위치의 포션을 꺼냅니다. 범위를 벗어나면 null 입니다.
        /// </summary>
        public PotionInstance TakeAt(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return null;
            }

            PotionInstance potion = _items[index];
            _items.RemoveAt(index);

#if UNITY_EDITOR
            Debug.Log($"[PotionInventory] : {potion.Data.potionName} 꺼냄 ({Count}/{Capacity})");
#endif

            OnChanged?.Invoke();
            return potion;
        }

        /// <summary>
        /// 가장 먼저 들어온 포션을 꺼냅니다. 비어 있으면 null 입니다.
        /// </summary>
        public PotionInstance TakeFirst()
        {
            return TakeAt(0);
        }
    }
}
