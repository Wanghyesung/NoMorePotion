using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 시체 상자의 화면 오브젝트(CorpseChest)를 만들고 없애는 컴포넌트입니다. 씬에 한 개 둡니다. (GroundPouchSpawner 와 같은 구조)
    /// 인벤토리 상태(TeamInventoryState)의 이벤트를 구독해서, 시체 상자가 생기면 죽은 위치에 만들고, 비워져서 사라지면 지웁니다.
    /// 늦게 들어와서 전체 목록(스냅샷)을 받았을 때(OnReloaded)는 모두 지우고 현재 상태대로 다시 만듭니다.
    /// 모든 클라이언트가 같은 상태를 받으므로 모두에게 같은 위치에 같은 상자가 보입니다.
    /// </summary>
    public class CorpseBoxSpawner : MonoBehaviour
    {
        #region Fields
        [SerializeField, LabelText("시체 상자 프리팹"), Required("CorpseChest 가 붙은 프리팹을 연결해 주세요.")]
        private CorpseChest _chestPrefab;

        [SerializeField, LabelText("상자를 모아 둘 부모 (선택)")]
        private Transform _parent;

        private readonly Dictionary<int, CorpseChest> _chests = new Dictionary<int, CorpseChest>();
        private TeamInventoryState _state;
        #endregion

        #region Lifecycle
        private void Start()
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;

            if (manager == null || manager.State == null)
            {
                Debug.LogWarning("[CorpseBoxSpawner] : InventoryNetworkSyncManager 가 없어 시체 상자를 만들 수 없습니다.");
                return;
            }

            _state = manager.State;
            _state.OnCorpseCreated += HandleCreated;
            _state.OnCorpseRemoved += HandleRemoved;
            _state.OnReloaded += HandleReloaded;

            // 이미 있는 시체 상자(이 컴포넌트보다 먼저 받은 상태)도 만들어 둡니다.
            RebuildAll();
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnCorpseCreated -= HandleCreated;
                _state.OnCorpseRemoved -= HandleRemoved;
                _state.OnReloaded -= HandleReloaded;
            }
        }
        #endregion

        #region Handlers
        private void HandleCreated(int corpseId)
        {
            if (_state.Corpses.TryGetValue(corpseId, out CorpseBox box))
            {
                Spawn(box);
            }
        }

        private void HandleRemoved(int corpseId)
        {
            if (_chests.TryGetValue(corpseId, out CorpseChest chest))
            {
                _chests.Remove(corpseId);

                if (chest != null)
                {
                    Destroy(chest.gameObject);
                }
            }
        }

        private void HandleReloaded()
        {
            RebuildAll();
        }

        private void RebuildAll()
        {
            foreach (KeyValuePair<int, CorpseChest> entry in _chests)
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value.gameObject);
                }
            }

            _chests.Clear();

            foreach (KeyValuePair<int, CorpseBox> entry in _state.Corpses)
            {
                Spawn(entry.Value);
            }
        }
        #endregion

        #region Spawn
        private void Spawn(CorpseBox box)
        {
            if (_chestPrefab == null || _chests.ContainsKey(box.Id))
            {
                return;
            }

            CorpseChest chest = Instantiate(_chestPrefab, box.Position, Quaternion.identity, _parent);

            // 모든 클라이언트에서 같은 방향이 되도록 상자 번호로 정합니다.
            chest.transform.rotation = Quaternion.Euler(0f, box.Id * 47 % 360, 0f);
            chest.Bind(box);
            _chests[box.Id] = chest;
        }
        #endregion
    }
}
