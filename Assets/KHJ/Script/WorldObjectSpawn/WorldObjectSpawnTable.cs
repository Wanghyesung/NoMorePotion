using System;
using System.Collections.Generic;
using Enzeun.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 한 지역에 자동 배치할 월드 오브젝트 후보 목록입니다. 가중치가 클수록 자주 뽑힙니다.
    /// 반경은 같은 지역에 놓일 다른 오브젝트와 떨어져야 하는 거리의 절반입니다. (두 오브젝트의 반경 합보다 가까이 놓이지 않음)
    /// 모든 클라이언트가 같은 결과를 내야 하므로 뽑기는 호출하는 쪽이 넘겨 주는 System.Random 으로만 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldObjectSpawnTable", menuName = "Enzeun/월드 오브젝트/자동 배치 테이블")]
    public class WorldObjectSpawnTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [SerializeField, LabelText("프리팹 (WorldObject)")]
            private WorldObject _prefab;

            [SerializeField, LabelText("가중치"), MinValue(0f)]
            private float _weight = 1f;

            [SerializeField, LabelText("반경 (m)"), MinValue(0f)]
            private float _radius = 1f;

            public WorldObject Prefab => _prefab;
            public float Weight => _weight;
            public float Radius => _radius;

            public bool IsValid => _prefab != null && _weight > 0f;
        }

        [SerializeField, LabelText("후보 목록")]
        private List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>
        /// 가중치로 후보 하나를 뽑아 목록 번호를 돌려줍니다. 뽑을 수 있는 후보가 없으면 -1 입니다.
        /// </summary>
        public int PickIndex(System.Random rng)
        {
            double total = 0d;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null && _entries[i].IsValid)
                {
                    total += _entries[i].Weight;
                }
            }

            if (total <= 0d)
            {
                return -1;
            }

            double roll = rng.NextDouble() * total;
            int last = -1;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] == null || !_entries[i].IsValid)
                {
                    continue;
                }

                last = i;
                roll -= _entries[i].Weight;

                if (roll < 0d)
                {
                    return i;
                }
            }

            return last;
        }
    }
}
