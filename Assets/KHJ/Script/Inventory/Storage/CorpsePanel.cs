using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 시체 상자 패널의 내용 표시와 꺼내기를 맡습니다. 열어 둔 시체 상자(StorageSession.OpenCorpseId)의 칸을 아이콘으로 그리고, 칸을 왼쪽 클릭하면 그 아이템을 내 개인 인벤으로 옮깁니다.
    ///
    /// 꺼내기 전용 : 시체 상자에는 넣을 수 없습니다. (이 패널에는 넣는 동작이 없음)
    /// 개인 인벤이 가득 차면 서버가 거부해서 못 꺼냅니다. (결과가 Full)
    /// 마지막 아이템을 꺼내서 상자가 사라지면 인벤토리를 강제로 닫습니다. (다른 사람이 마지막 것을 꺼낸 경우도 같음)
    ///
    /// 설치 : 항상 켜져 있는 오브젝트에 붙이세요. (예: 인벤토리 캔버스 루트) 시체 상자 패널(RootBoxSideBackground)은 꺼져 있을 때가 있어서 거기에 두면 열린 상자를 놓칩니다.
    /// 칸 아이콘은 인벤토리 컨트롤러의 아이콘 프리팹으로 "칸 부모" 아래에 만듭니다. (칸 수는 시체 상자의 칸 수를 따름)
    /// </summary>
    public class CorpsePanel : MonoBehaviour
    {
        #region Config
        [SerializeField, Required, LabelText("칸 아이콘이 생길 부모 (Grid)")]
        private RectTransform _content;

        [SerializeField, LabelText("인벤토리 컨트롤러 (비우면 부모에서 찾음)")]
        private InventoryController _controller;
        #endregion

        #region State
        // 칸 번호 = 목록 번호입니다. (시체 상자의 칸 0, 1, 2 ... 에 1:1 로 묶임)
        private readonly List<InventoryIcon> _icons = new List<InventoryIcon>();
        private TeamInventoryState _state;

        // 지금 그리고 있는 시체 상자 번호입니다. 0 이면 없음
        private int _shownId;
        #endregion

        #region Lifecycle
        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponentInParent<InventoryController>(true);
            }
        }

        private void Start()
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;

            if (manager == null || manager.State == null)
            {
                Debug.LogWarning("[CorpsePanel] : InventoryNetworkSyncManager 가 없어 시체 상자를 보여 줄 수 없습니다.");
                return;
            }

            _state = manager.State;
            _state.OnCorpseChanged += HandleChanged;
            _state.OnCorpseRemoved += HandleRemoved;
            _state.OnReloaded += HandleReloaded;
            StorageSession.OnCorpseSessionChanged += HandleSession;

            // 이 컴포넌트가 시작하기 전에 이미 열린 상자가 있으면 바로 보여 줍니다.
            HandleSession(StorageSession.OpenCorpseId);
        }

        private void OnDestroy()
        {
            StorageSession.OnCorpseSessionChanged -= HandleSession;

            if (_state != null)
            {
                _state.OnCorpseChanged -= HandleChanged;
                _state.OnCorpseRemoved -= HandleRemoved;
                _state.OnReloaded -= HandleReloaded;
            }
        }
        #endregion

        #region Show
        // 열어 둔 상자가 바뀌었을 때(0 이면 닫음)
        private void HandleSession(int corpseId)
        {
            _shownId = corpseId;

            if (corpseId == 0 || _state == null)
            {
                return;
            }

            Refresh();
        }

        private void HandleChanged(int corpseId)
        {
            // 마지막 아이템을 꺼낸 같은 변경에서 상자가 이미 사라졌을 수 있습니다. (사라진 상자는 Refresh 가 건너뜀)
            if (corpseId == _shownId)
            {
                Refresh();
            }
        }

        private void HandleRemoved(int corpseId)
        {
            // 보고 있던 상자가 비워져서 사라졌습니다. (내가 마지막 아이템을 꺼냈거나 다른 사람이 꺼냄)
            if (corpseId == _shownId && _shownId != 0)
            {
                ForceCloseAsync().Forget();
            }
        }

        private void HandleReloaded()
        {
            if (_shownId == 0)
            {
                return;
            }

            // 전체 목록을 새로 받았을 때, 보던 상자가 없어졌으면 닫고 있으면 다시 그립니다.
            if (_state.Corpses.ContainsKey(_shownId))
            {
                Refresh();
            }
            else
            {
                ForceCloseAsync().Forget();
            }
        }

        // 열어 둔 상자의 칸을 아이콘에 그립니다. 상자가 없으면 아무것도 하지 않습니다.
        private void Refresh()
        {
            if (_shownId == 0 || _state == null || !_state.Corpses.TryGetValue(_shownId, out CorpseBox box))
            {
                return;
            }

            EnsureIcons(box.Slots.Capacity);

            for (int i = 0; i < _icons.Count; i++)
            {
                bool inUse = i < box.Slots.Capacity;
                _icons[i].gameObject.SetActive(inUse);

                if (inUse)
                {
                    _icons[i].ShowSlot(box.Slots.Slots[i]);
                }
            }
        }

        // 칸 수만큼 아이콘을 만들어 둡니다. 한 번 만든 아이콘은 재사용합니다.
        private void EnsureIcons(int count)
        {
            if (_controller == null || _controller.iconPrefab == null)
            {
                Debug.LogWarning("[CorpsePanel] : 인벤토리 컨트롤러 또는 아이콘 프리팹이 없어 칸을 만들 수 없습니다.");
                return;
            }

            while (_icons.Count < count)
            {
                InventoryIcon icon = Instantiate(_controller.iconPrefab, _content);
                icon.OnClicked += HandleClicked;
                _icons.Add(icon);
            }
        }
        #endregion

        #region Take
        // 칸을 왼쪽 클릭하면 그 아이템을 내 개인 인벤으로 옮깁니다.
        private void HandleClicked(InventoryIcon icon)
        {
            int slot = _icons.IndexOf(icon);
            if (slot < 0 || _shownId == 0)
            {
                return;
            }

            ItemInstance item = icon.Item;
            if (item == null || item.ItemCount <= 0)
            {
                return;
            }

            TakeAsync(_shownId, slot).Forget();
        }

        private async UniTaskVoid TakeAsync(int corpseId, int slot)
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            if (manager == null || manager.LocalActor <= 0)
            {
                return;
            }

            // 칸 전체를 개인 인벤의 알맞은 칸으로 옮깁니다. 개인 인벤에 빈 칸이 없으면 서버가 Full 로 거부합니다.
            InventoryResult result = await manager.TransferAsync(ContainerRef.Corpse(corpseId, slot), ContainerRef.Personal(manager.LocalActor));

            if (!result.Success)
            {
                // Full : 내 개인 인벤이 가득 참, NotFound/SlotEmpty : 그 사이에 누가 꺼감. 화면 알림은 UI 담당과 정한 뒤에 붙입니다.
                Debug.LogWarning($"[CorpsePanel]: 시체 상자에서 꺼내지 못했습니다 ({result.Reason})");
            }
        }
        #endregion

        #region Close
        /// <summary>
        /// 열어 둔 상자가 사라졌을 때 인벤토리를 강제로 닫고 시체 상자 이용을 끝냅니다.
        /// 상자 오브젝트(CorpseChest)는 같은 시점에 지워져서 스스로 닫을 수 없으므로, 항상 살아 있는 이 패널이 맡습니다.
        /// </summary>
        private async UniTaskVoid ForceCloseAsync()
        {
            _shownId = 0;
            StorageSession.SetOpenCorpse(0);

            InventoryController controller = _controller != null ? _controller : FindFirstObjectByType<InventoryController>();
            if (controller == null)
            {
                return;
            }

            // 인벤토리가 열려 있을 때(플레이어 조작이 꺼져 있을 때)만 닫습니다. 이미 닫혀 있는데 닫기를 부르면 오히려 열립니다. (같은 키로 여닫는 방식)
            if (InputManager.Instance != null && !InputManager.Instance.HasGameplayInput)
            {
                controller.OnTabClosed();
            }

            // 인벤토리가 페이드 아웃하는 동안 기다렸다가 옆 패널을 접습니다. 그 사이에 다른 것을 열었다면 접지 않습니다.
            bool cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(JJH_Constants.COMMON_FADE_TIME), cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();

            if (!cancelled && !StorageSession.IsAnyOpen)
            {
                controller.InventorySideClose();
            }
        }
        #endregion
    }
}
