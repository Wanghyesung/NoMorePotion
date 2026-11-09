using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 바닥 재료 주머니의 화면 오브젝트(GroundPouch)를 만들고 없애는 컴포넌트입니다. 씬에 한 개 두거나 InventoryNetworkSyncManager 프리팹에 함께 둡니다.
    /// 인벤토리 상태(TeamInventoryState)의 이벤트를 구독해서, 주머니가 생기면 만들고(마스터가 정한 위치에), 개수가 바뀌면 갱신하고, 없어지면 지웁니다.
    /// 늦게 들어와서 전체 목록(스냅샷)을 받았을 때(OnReloaded)는 모두 지우고 현재 상태대로 다시 만듭니다.
    /// 모든 클라이언트가 같은 상태를 받으므로 모두에게 같은 위치에 같은 주머니가 보입니다.
    /// </summary>
    public class GroundPouchSpawner : MonoBehaviour
    {
        [SerializeField, LabelText("주머니 프리팹"), Required("GroundPouch 가 붙은 프리팹을 연결해 주세요.")]
        private GroundPouch _pouchPrefab;

        [SerializeField, LabelText("주머니를 모아 둘 부모 (선택)")]
        private Transform _parent;

        private readonly Dictionary<int, GroundPouch> _pouches = new Dictionary<int, GroundPouch>();
        private TeamInventoryState _state;

        private void Start()
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;

            if (manager == null || manager.State == null)
            {
                Debug.LogWarning("[GroundPouchSpawner] : InventoryNetworkSyncManager 가 없어 바닥 주머니를 만들 수 없습니다.");
                return;
            }

            _state = manager.State;
            _state.OnGroundAdded += HandleAdded;
            _state.OnGroundChanged += HandleChanged;
            _state.OnGroundRemoved += HandleRemoved;
            _state.OnReloaded += HandleReloaded;

            // 이미 바닥에 있는 주머니(이 컴포넌트보다 먼저 받은 상태)도 만들어 둡니다.
            RebuildAll();
        }

        private void OnDestroy()
        {
            if (_state != null)
            {
                _state.OnGroundAdded -= HandleAdded;
                _state.OnGroundChanged -= HandleChanged;
                _state.OnGroundRemoved -= HandleRemoved;
                _state.OnReloaded -= HandleReloaded;
            }
        }

        private void HandleAdded(int dropId)
        {
            if (_state.GroundDrops.TryGetValue(dropId, out GroundDrop drop))
            {
                Spawn(drop);
            }
        }

        private void HandleChanged(int dropId)
        {
            if (_pouches.TryGetValue(dropId, out GroundPouch pouch) && _state.GroundDrops.TryGetValue(dropId, out GroundDrop drop))
            {
                pouch.Refresh(drop);
            }
        }

        private void HandleRemoved(int dropId)
        {
            if (_pouches.TryGetValue(dropId, out GroundPouch pouch))
            {
                _pouches.Remove(dropId);

                if (pouch != null)
                {
                    Destroy(pouch.gameObject);
                }
            }
        }

        private void HandleReloaded()
        {
            RebuildAll();
        }

        private void RebuildAll()
        {
            foreach (KeyValuePair<int, GroundPouch> entry in _pouches)
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value.gameObject);
                }
            }

            _pouches.Clear();

            foreach (KeyValuePair<int, GroundDrop> entry in _state.GroundDrops)
            {
                Spawn(entry.Value);
            }
        }

        private void Spawn(GroundDrop drop)
        {
            if (_pouchPrefab == null || _pouches.ContainsKey(drop.Id))
            {
                return;
            }

            GroundPouch pouch = Instantiate(_pouchPrefab, drop.Position, Quaternion.identity, _parent);

            // 모든 클라이언트에서 같은 방향이 되도록 주머니 번호로 정합니다. (같은 모양이 줄지어 서 보이지 않게)
            pouch.transform.rotation = Quaternion.Euler(0f, drop.Id * 47 % 360, 0f);
            pouch.Bind(drop);
            _pouches[drop.Id] = pouch;
        }
    }
}
