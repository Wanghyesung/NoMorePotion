using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Photon.Pun;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 내 개인 인벤토리를 UI 가 안전하게 읽을 수 있게 해 주는 도우미입니다. UI 는 이 클래스의 이벤트만 구독하면 됩니다.
    /// 개인 인벤토리는 마스터가 처음 아이템을 넣을 때 만들어지고, 늦게 들어오면 전체 목록을 받으면서 객체가 통째로 바뀌므로,
    /// 인벤토리 객체를 직접 들고 있으면 연결이 끊깁니다. 이 클래스가 그때마다 다시 연결하고 알려 줍니다.
    ///
    /// 사용 : view = new LocalPersonalInventoryView(); await view.StartAsync(token); (OnDestroy 에서 view.Dispose())
    /// - OnRebound : 연결 대상이 바뀜 (처음 연결, 만들어짐, 전체 목록 수신). 모든 칸을 다시 그리세요. Current 가 null 일 수 있음(아직 없음 = 모든 칸이 빈 것)
    /// - OnSlotChanged : 칸 하나가 바뀜. 그 칸만 다시 그리세요.
    /// 조작(이동, 꺼내기 등)은 이 클래스가 아니라 InventoryNetworkSyncManager 의 요청 함수로 보냅니다.
    /// </summary>
    public sealed class LocalPersonalInventoryView : IDisposable
    {
        private TeamInventoryState _state;
        private PersonalInventory _current;
        private int _actor;

        /// <summary>
        /// 지금 연결된 내 개인 인벤토리입니다. 아직 만들어지기 전이면 null 입니다.
        /// </summary>
        public PersonalInventory Current => _current;

        /// <summary>
        /// 내 칸 수입니다. 아직 인벤토리가 없어도 기본 칸 수를 돌려줍니다.
        /// </summary>
        public int Capacity => _current != null ? _current.Capacity : PersonalInventory.DefaultCapacity;

        public event Action OnRebound;
        public event Action<int> OnSlotChanged;

        /// <summary>
        /// 방에 들어가고 인벤토리 매니저가 준비될 때까지 기다린 뒤 연결합니다. 취소되면 아무것도 연결하지 않고 끝납니다.
        /// </summary>
        public async UniTask StartAsync(CancellationToken token)
        {
            bool canceled = await UniTask.WaitUntil(
                    () => PhotonNetwork.InRoom && InventoryNetworkSyncManager.Instance != null && InventoryNetworkSyncManager.Instance.State != null,
                    cancellationToken: token)
                .SuppressCancellationThrow();

            if (canceled || _state != null)
            {
                return;
            }

            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            _state = manager.State;
            _actor = manager.LocalActor;

            _state.OnPersonalAdded += HandlePersonalAdded;
            _state.OnPersonalRemoved += HandlePersonalRemoved;
            _state.OnReloaded += HandleReloaded;

            Rebind();
        }

        public void Dispose()
        {
            if (_state != null)
            {
                _state.OnPersonalAdded -= HandlePersonalAdded;
                _state.OnPersonalRemoved -= HandlePersonalRemoved;
                _state.OnReloaded -= HandleReloaded;
                _state = null;
            }

            Detach();
        }

        private void HandlePersonalAdded(int actor)
        {
            // 새 개인 인벤토리가 만들어진 직후라 첫 칸 변경은 이어서 옵니다.
            if (actor == _actor)
            {
                Rebind();
            }
        }

        private void HandlePersonalRemoved(int actor)
        {
            if (actor == _actor)
            {
                Rebind();
            }
        }

        private void HandleReloaded()
        {
            Rebind();
        }

        private void Rebind()
        {
            Detach();

            _current = _state != null ? _state.GetPersonal(_actor) : null;

            if (_current != null)
            {
                _current.OnSlotChanged += HandleSlotChanged;
            }

            OnRebound?.Invoke();
        }

        private void Detach()
        {
            if (_current != null)
            {
                _current.OnSlotChanged -= HandleSlotChanged;
                _current = null;
            }
        }

        private void HandleSlotChanged(int slot)
        {
            OnSlotChanged?.Invoke(slot);
        }
    }
}
