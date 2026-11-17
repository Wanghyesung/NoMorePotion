using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 아이콘 목록(칸들의 부모)에 붙여서, 아이콘을 왼쪽 클릭하면 공용 인벤 ↔ 창고로 옮깁니다. 창고를 이용 중(StorageSession.IsOpen)일 때만 동작합니다.
    ///
    /// 설치 : 공용 인벤의 재료 목록과 포션 목록, 창고 패널의 재료 목록과 포션 목록(아이콘이 자식으로 생기는 RectTransform)에 각각 붙이고, '이 목록이 보여 주는 곳'을 맞춥니다.
    /// - 포션 : 클릭한 포션 한 개를 옮깁니다.
    /// - 재료 : 클릭하면 설정한 개수만큼, Shift + 클릭이면 전부 옮깁니다. 받는 곳이 가득 차면 들어가는 만큼만 옮겨집니다.
    ///
    /// 기존 아이콘 코드(InventoryIcon, PublicInventory)를 고치지 않고, 아이콘이 자식으로 생길 때 그 클릭 이벤트(OnClicked)를 구독하는 방식입니다.
    /// 오른쪽 클릭(조합창에 올리기)은 건드리지 않습니다.
    /// </summary>
    public class InventoryTransferRelay : MonoBehaviour
    {
        #region Types
        public enum Side
        {
            Pack,
            Storage,
        }
        #endregion

        #region Config
        [SerializeField, LabelText("이 목록이 보여 주는 곳")]
        private Side _side = Side.Pack;

        [SerializeField, LabelText("재료 클릭 한 번에 옮길 개수"), MinValue(1)]
        private int _materialClickCount = 10;
        #endregion

        #region Subscribe
        private readonly HashSet<InventoryIcon> _subscribed = new HashSet<InventoryIcon>();

        private void Start()
        {
            ScanIcons();
        }

        // 꺼져 있던 탭이 켜질 때, 그 사이에 생긴 아이콘을 구독합니다. (꺼진 오브젝트에는 자식 변경 알림이 오지 않음)
        private void OnEnable()
        {
            ScanIcons();
        }

        // 아이콘이 새로 생기거나 사라질 때 호출됩니다.
        private void OnTransformChildrenChanged()
        {
            ScanIcons();
        }

        private void OnDestroy()
        {
            foreach (InventoryIcon icon in _subscribed)
            {
                if (icon != null)
                {
                    icon.OnClicked -= HandleClicked;
                }
            }

            _subscribed.Clear();
        }

        private void ScanIcons()
        {
            // 사라진 아이콘은 목록에서 뺍니다.
            _subscribed.RemoveWhere(icon => icon == null);

            InventoryIcon[] icons = GetComponentsInChildren<InventoryIcon>(true);
            for (int i = 0; i < icons.Length; i++)
            {
                if (_subscribed.Add(icons[i]))
                {
                    icons[i].OnClicked += HandleClicked;
                }
            }
        }
        #endregion

        #region Move
        private void HandleClicked(InventoryIcon icon)
        {
            // 창고 상자에서 연 동안만 옮깁니다.
            if (!StorageSession.IsOpen || icon == null)
            {
                return;
            }

            ItemInstance item = icon.Item;
            if (item == null || item.ItemCount <= 0)
            {
                return;
            }

            MoveAsync(item).Forget();
        }

        private async UniTaskVoid MoveAsync(ItemInstance item)
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            if (manager == null)
            {
                return;
            }

            ContainerRef from = _side == Side.Pack ? ContainerRef.PackRef : ContainerRef.StorageRef;
            ContainerRef to = _side == Side.Pack ? ContainerRef.StorageRef : ContainerRef.PackRef;

            InventoryResult result;
            if (item.kind == InventorySlotKind.Potion)
            {
                // 포션은 인스턴스 번호로 가리킵니다.
                result = await manager.TransferAsync(from, to, instanceId: item.instanceId);
            }
            else
            {
                bool all = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                int count = all ? item.ItemCount : Mathf.Min(_materialClickCount, item.ItemCount);
                result = await manager.TransferAsync(from, to, materialId: item.itemId, count: count);
            }

            if (!result.Success)
            {
                // 가득 참, 접근 불가 등. 화면 알림은 UI 담당과 정한 뒤에 붙입니다.
                Debug.LogWarning($"[InventoryTransferRelay]: {from.Kind} -> {to.Kind} 이동 실패 ({result.Reason})");
            }
        }
        #endregion
    }
}
