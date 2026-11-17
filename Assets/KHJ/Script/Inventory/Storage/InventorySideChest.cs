using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 옆 패널(창고, 시체 상자)을 여는 상자의 공통 동작입니다. 내 플레이어가 트리거 범위 안에서 상호작용 키(E)를 누르면 인벤토리를 열고 옆 패널을 펼칩니다.
    /// 인벤토리를 닫으면(Tab, 닫기 버튼) 패널도 접히고 이용이 끝납니다.
    ///
    /// 설치 : 상자 오브젝트에 이 클래스를 상속한 컴포넌트(StorageChest, CorpseChest)와 Is Trigger 콜라이더를 둡니다. (범위 = 트리거 크기, 컴포넌트는 콜라이더와 같은 오브젝트에 있어야 트리거 이벤트를 받습니다)
    /// 상호작용 키 처리는 플레이어의 줍기(PlayerPickState)와 별개로 InputManager.InteractAction 을 직접 구독합니다. (기존 코드를 고치지 않기 위함)
    ///
    /// 상속하는 쪽이 정할 것 : 어느 패널을 펼치는지(UsesStoragePanel), 열 때(TryBeginSession)와 끝날 때(EndSession) StorageSession 을 어떻게 바꾸는지.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class InventorySideChest : MonoBehaviour
    {
        #region Config
        [SerializeField, LabelText("인벤토리 컨트롤러 (비우면 씬에서 찾음)")]
        private InventoryController _controller;
        #endregion

        #region Hooks
        /// <summary>
        /// true 면 창고 패널, false 면 시체 상자 패널을 펼칩니다. (InventoryController.InvenSideAnimation 의 type)
        /// </summary>
        protected abstract bool UsesStoragePanel { get; }

        /// <summary>
        /// 이용을 시작합니다. StorageSession 을 이 상자의 것으로 바꾸고 true 를 돌려줍니다. 열 수 없으면(예: 상자가 이미 사라짐) false 를 돌려주면 열지 않습니다.
        /// </summary>
        protected abstract bool TryBeginSession();

        /// <summary>
        /// 이용이 끝났을 때(인벤토리를 닫았거나 상자가 사라졌을 때) StorageSession 을 되돌립니다.
        /// </summary>
        protected abstract void EndSession();
        #endregion

        #region Debug
        // 플레이 중 인스펙터에서 지금 상태를 볼 수 있게 합니다. (안 열릴 때 어느 단계에서 막히는지 확인용)
        [ShowInInspector, ReadOnly, LabelText("범위 안의 내 플레이어 콜라이더 수"), FoldoutGroup("상태 확인")]
        private int DebugInRangeCount => _playerColliders.Count;

        [ShowInInspector, ReadOnly, LabelText("상호작용 입력 구독 중"), FoldoutGroup("상태 확인")]
        private bool DebugSubscribed => _subscribed;

        [ShowInInspector, ReadOnly, LabelText("옆 패널 이용 중 (StorageSession)"), FoldoutGroup("상태 확인")]
        private bool DebugSessionOpen => StorageSession.IsAnyOpen;

        [ShowInInspector, ReadOnly, LabelText("플레이어 조작 가능 (HasGameplayInput)"), FoldoutGroup("상태 확인")]
        private bool DebugHasInput => InputManager.Instance != null && InputManager.Instance.HasGameplayInput;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        protected void Log(string message)
        {
            Debug.Log($"[{GetType().Name}] : {message}", this);
        }
        #endregion

        #region Range
        // 범위 안에 있는 내 플레이어의 콜라이더들입니다. 플레이어 콜라이더가 여러 개라도 모두 나갈 때까지 범위 안으로 칩니다.
        private readonly HashSet<Collider> _playerColliders = new HashSet<Collider>();
        private bool _subscribed;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsLocalPlayerCollider(other))
            {
                return;
            }

            _playerColliders.Add(other);
            Subscribe();
            Log($"범위 안으로 들어왔습니다. (콜라이더 {other.name}, 합계 {_playerColliders.Count}개)");
        }

        private void OnTriggerExit(Collider other)
        {
            if (_playerColliders.Remove(other))
            {
                Log($"범위를 벗어났습니다. (콜라이더 {other.name}, 남은 {_playerColliders.Count}개)");

                if (_playerColliders.Count == 0)
                {
                    Unsubscribe();
                }
            }
        }

        protected virtual void OnDisable()
        {
            _playerColliders.Clear();
            Unsubscribe();
        }

        private static bool IsLocalPlayerCollider(Collider other)
        {
            Player player = other.GetComponentInParent<Player>();
            return player != null && player.IsLocalPlayer;
        }

        private void Subscribe()
        {
            if (_subscribed || InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.InteractAction.performed += OnInteract;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            // 입력 매니저가 먼저 사라진 경우에는 해제할 대상이 없습니다.
            if (InputManager.Instance != null)
            {
                InputManager.Instance.InteractAction.performed -= OnInteract;
            }

            _subscribed = false;
        }
        #endregion

        #region Open
        private void OnInteract(InputAction.CallbackContext context)
        {
            InputManager input = InputManager.Instance;

            // 인벤토리가 열려 있으면 플레이어 입력이 꺼져 있습니다. (상호작용 입력도 꺼져서 보통은 오지 않음)
            if (input == null || !input.HasGameplayInput)
            {
                Log("상호작용을 무시합니다: 플레이어 조작이 꺼져 있습니다.");
                return;
            }

            // 조작이 켜져 있는데 이용 중으로 남아 있으면 인벤토리는 이미 닫힌 것입니다. 닫힘 감지를 놓친 경우를 스스로 바로잡습니다.
            if (StorageSession.IsAnyOpen)
            {
                Log("이용 상태가 남아 있어 초기화합니다.");
                StorageSession.ClearAll();
            }

            // 범위를 벗어났는데 파괴된 콜라이더가 남아 있을 수 있으므로 비웁니다. (파괴된 콜라이더는 Exit 가 안 올 수 있음)
            _playerColliders.RemoveWhere(collider => collider == null);
            if (_playerColliders.Count == 0)
            {
                Log("상호작용을 무시합니다: 범위 안에 내 플레이어가 없습니다.");
                Unsubscribe();
                return;
            }

            OpenAsync().Forget();
        }

        private async UniTaskVoid OpenAsync()
        {
            InventoryController controller = _controller != null ? _controller : FindFirstObjectByType<InventoryController>();
            if (controller == null)
            {
                Debug.LogWarning($"[{GetType().Name}]: 씬에 InventoryController 가 없어 열 수 없습니다.", this);
                return;
            }

            if (!TryBeginSession())
            {
                Log("열 수 없는 상태라 열지 않습니다.");
                return;
            }

            var token = this.GetCancellationTokenOnDestroy();
            Log("엽니다.");

            // 인벤토리가 닫힌 상태에서만 상호작용이 들어오므로(열리면 상호작용 입력이 꺼짐), 한 번 부르면 열립니다.
            // InventoryActive 는 열자마자(대기 전에) 플레이어 입력을 끕니다. 아래 대기는 그 뒤에 시작합니다.
            controller.InventoryActive(token).Forget();
            controller.InvenSideAnimation(token, true, true, UsesStoragePanel).Forget();

            // 인벤토리를 닫을 때까지 기다립니다. 인벤토리를 열면 플레이어 입력이 꺼지고 닫으면 다시 켜지므로, 입력이 돌아오면 닫기 시작한 것입니다.
            // (인벤토리 캔버스가 꺼졌는지로 판단하면 루트에도 캔버스가 있어서 엉뚱한 것을 볼 수 있습니다.)
            bool cancelled = await UniTask.WaitUntil(
                    () => InputManager.Instance == null || InputManager.Instance.HasGameplayInput,
                    cancellationToken: token)
                .SuppressCancellationThrow();

            // 이 시점부터 이동이 멈추고, 다시 상자를 열 수 있습니다.
            EndSession();
            Log($"이용을 끝냅니다. (취소됨: {cancelled})");

            if (cancelled)
            {
                return;
            }

            // 인벤토리가 페이드 아웃하는 동안은 그대로 두었다가 접습니다. 접는 사이에 상자를 다시 열었다면 접지 않습니다.
            bool fadeCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(JJH_Constants.COMMON_FADE_TIME), cancellationToken: token)
                .SuppressCancellationThrow();

            if (!fadeCancelled && !StorageSession.IsAnyOpen && controller != null)
            {
                // 다음에 일반 인벤토리를 열 때 패널이 펼쳐진 채로 나오지 않도록 접습니다.
                controller.InventorySideClose();
            }
        }
        #endregion
    }
}
