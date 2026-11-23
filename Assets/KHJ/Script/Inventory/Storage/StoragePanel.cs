using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 창고 패널의 내용 표시를 창고 데이터에 연결합니다. 창고 패널 안의 PublicInventory 를 공용 인벤 대신 창고(TeamInventoryState.Storage)에 묶습니다.
    /// PublicInventory 는 시작할 때 공용 인벤에 스스로 묶는데, 이 컴포넌트가 그 뒤에 창고로 다시 묶습니다. (어느 쪽이 먼저 실행돼도 결과는 창고)
    ///
    /// 주의 : PublicInventory 의 탭 전환(SlotChange)은 InventoryController 의 공용 스크롤을 바꾸므로, 창고 패널의 탭 버튼에는 연결하지 마세요.
    /// 재료와 포션 목록을 둘 다 켜 두고 각자 스크롤을 두는 구성을 권합니다.
    /// </summary>
    public class StoragePanel : MonoBehaviour
    {
        #region Config
        [SerializeField, Required, LabelText("창고 내용을 보여 줄 PublicInventory")]
        private PublicInventory _inventory;
        #endregion

        #region Bind
        // 패널이 처음 켜질 때 시작됩니다. (꺼진 채로 시작하는 패널은 처음 열릴 때 묶임)
        private void Start()
        {
            // 창고 재료는 조합에 쓸 수 없으므로 조합 짝 강조를 끕니다.
            if (_inventory != null)
            {
                _inventory.SetCombineHighlightEnabled(false);
            }

            BindAsync().Forget();
        }

        private async UniTaskVoid BindAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            bool cancelled = await UniTask.WaitUntil(
                    () => InventoryNetworkSyncManager.Instance != null && InventoryNetworkSyncManager.Instance.State != null,
                    cancellationToken: token)
                .SuppressCancellationThrow();

            if (cancelled || _inventory == null)
            {
                return;
            }

            TeamInventoryState state = InventoryNetworkSyncManager.Instance.State;
            _inventory.Bind(state.Storage.Materials, state.Storage.Potions);

#if UNITY_EDITOR
            Debug.Log("[StoragePanel] : 창고 패널을 창고 데이터에 연결했습니다.");
#endif
        }
        #endregion
    }
}
