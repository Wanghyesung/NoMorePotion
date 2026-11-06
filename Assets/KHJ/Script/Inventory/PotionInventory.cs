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

        public PotionInventory(int capacity = DefaultCapacity)
        {
            Capacity = Mathf.Max(1, capacity);
        }

        /// <summary>
        /// 보관할 수 있는 최대 개수입니다.
        /// </summary>
        public int Capacity { get; }

        private readonly List<PotionInstance> _items = new List<PotionInstance>();
        public int Count => _items.Count;
        public bool IsFull => _items.Count >= Capacity;

        /// <summary>
        /// 보관 중인 포션 목록입니다. (읽기 전용 · 넣고 빼는 것은 Add / TakeAt / TakeFirst / TakeRandom 로만)
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

            // ID 가 아직 없으면 발급하고, 이미 있으면 같은 ID 가 들어 있지 않은지 확인합니다.
            if (potion.InstanceId == 0)
            {
                potion.AssignInstanceId(PotionInstanceIds.Next());
            }
            else if (Contains(potion.InstanceId))
            {
                Debug.LogWarning($"[PotionInventory]: 같은 인스턴스 ID({potion.InstanceId})의 포션이 이미 있습니다.");
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
        /// 모든 포션을 비웁니다. 전체 목록을 새로 받아 적용하기 전에 사용합니다.
        /// </summary>
        public void Clear()
        {
            if (_items.Count == 0)
            {
                return;
            }

            _items.Clear();
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 해당 인스턴스 ID 의 포션이 들어 있는지 확인합니다.
        /// </summary>
        public bool Contains(int instanceId)
        {
            return IndexOf(instanceId) >= 0;
        }

        /// <summary>
        /// 해당 인스턴스 ID 의 포션 위치를 반환합니다. 없으면 -1 입니다.
        /// </summary>
        public int IndexOf(int instanceId)
        {
            if (instanceId <= 0)
            {
                return -1;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].InstanceId == instanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 인스턴스 ID 로 포션을 꺼냅니다. 없으면 null 입니다.
        /// 멀티플레이에서는 위치(index)가 사람마다 어긋날 수 있어서, 꺼내기는 이 메서드(ID 기준)를 사용합니다.
        /// </summary>
        public PotionInstance Take(int instanceId)
        {
            return TakeAt(IndexOf(instanceId));
        }

        /// <summary>
        /// 가장 먼저 들어온 포션을 꺼냅니다. 비어 있으면 null 입니다.
        /// </summary>
        public PotionInstance TakeFirst()
        {
            return TakeAt(0);
        }

        /// <summary>
        /// 무작위 포션을 꺼냅니다. 비어 있으면 null 입니다.
        /// </summary>
        /// <returns></returns>
        public PotionInstance TakeRandom()
        {
            return TakeAt(UnityEngine.Random.Range(0, Count));
        }
    }
}
