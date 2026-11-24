using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 아이콘 목록(칸들의 부모)에 붙여서, 아이콘을 왼쪽 클릭하면 아이템을 옮깁니다.
    /// - 개인 인벤 : 창고를 열었든 아니든 항상 공용 인벤으로
    /// - 창고를 이용 중(StorageSession.IsOpen) : 공용 인벤 → 창고, 창고 → 공용 인벤
    /// - 창고를 열지 않았을 때 : 공용 인벤의 포션만 내 개인 인벤으로 (재료는 옮기지 않음)
    ///
    /// 설치 : 아래 목록(아이콘이 자식으로 있는 RectTransform 또는 그 부모)에 각각 붙이고, '이 목록이 보여 주는 곳'을 맞춥니다.
    /// - 공용 인벤의 재료 목록, 포션 목록 : Pack
    /// - 창고 패널의 재료 목록, 포션 목록 : Storage
    /// - 개인 인벤 칸들(10칸)의 공통 부모 : Personal
    /// 옮기는 양 : 포션은 한 개, 재료는 설정한 개수만큼, Shift + 클릭이면 전부입니다. 받는 곳이 가득 차면 들어가는 만큼만 옮겨집니다.
    ///
    /// 기존 아이콘 코드(InventoryIcon, PublicInventory, PrivateInventory)를 고치지 않습니다.
    /// - 공용 인벤, 창고의 아이콘 : 아이콘이 자식으로 생길 때 그 클릭 이벤트(OnClicked)를 구독합니다.
    /// - 개인 인벤 칸 : Button 이 없어 OnClicked 가 오지 않으므로, 칸마다 클릭 도우미(InventoryIconClickProxy)를 자동으로 붙여서 받습니다.
    /// 오른쪽 클릭(조합창에 올리기)은 건드리지 않습니다.
    /// </summary>
    public class InventoryTransferRelay : MonoBehaviour
    {
        // 내 클릭으로 요청한 포션 이동이 성공했을 때 목적지를 알린다.
        public event System.Action<ContainerKind> OnPotionTransferred;

        #region Types
        public enum Side
        {
            Pack,
            Storage,
            Personal,
        }
        #endregion

        #region Config
        [SerializeField, LabelText("이 목록이 보여 주는 곳")]
        private Side _side = Side.Pack;

        [SerializeField, LabelText("재료 클릭 한 번에 옮길 개수"), MinValue(1)]
        private int _materialClickCount = 10;

        // 창고를 열지 않았을 때, 공용 인벤의 포션을 클릭하면 내 개인 인벤으로 옮깁니다. (재료는 옮기지 않음)
        [SerializeField, LabelText("창고를 열지 않았을 때 공용 인벤 포션 클릭 → 개인 인벤")]
        private bool _packPotionToPersonal = true;

        // 개인 인벤 칸은 Button 이 없어서 눌림 연출이 없으므로, 클릭 도우미가 대신 보여 줍니다. (Personal 일 때만 사용)
        [SerializeField, LabelText("개인 칸 눌림 이미지 (비우면 색을 어둡게)"), ShowIf("_side", Side.Personal)]
        private Sprite _personalPressedSprite;
        #endregion

        #region Subscribe
        // OnClicked 를 구독한 아이콘들 (공용 인벤, 창고)
        private readonly HashSet<InventoryIcon> _subscribed = new HashSet<InventoryIcon>();

        // 클릭 도우미를 구독한 칸들 (개인 인벤)
        private readonly HashSet<InventoryIconClickProxy> _proxies = new HashSet<InventoryIconClickProxy>();

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

            foreach (InventoryIconClickProxy proxy in _proxies)
            {
                if (proxy != null)
                {
                    proxy.LeftClicked -= HandleClicked;
                }
            }

            _subscribed.Clear();
            _proxies.Clear();
        }

        private void ScanIcons()
        {
            // 사라진 아이콘은 목록에서 뺍니다.
            _subscribed.RemoveWhere(icon => icon == null);
            _proxies.RemoveWhere(proxy => proxy == null);

            InventoryIcon[] icons = GetComponentsInChildren<InventoryIcon>(true);
            for (int i = 0; i < icons.Length; i++)
            {
                if (_side == Side.Personal)
                {
                    // 개인 인벤 칸은 Button 이 없어 OnClicked 가 오지 않습니다. 클릭 도우미를 붙여서 받습니다.
                    if (!icons[i].TryGetComponent(out InventoryIconClickProxy proxy))
                    {
                        proxy = icons[i].gameObject.AddComponent<InventoryIconClickProxy>();
                    }

                    proxy.Configure(_personalPressedSprite);

                    if (_proxies.Add(proxy))
                    {
                        proxy.LeftClicked += HandleClicked;
                        Log($"개인 칸 '{icons[i].name}'(칸 {icons[i].slotNum})에 클릭 도우미를 붙였습니다.");
                    }
                }
                else if (_subscribed.Add(icons[i]))
                {
                    icons[i].OnClicked += HandleClicked;
                }
            }
        }
        #endregion

        #region Move
        private void HandleClicked(InventoryIcon icon)
        {
            if (icon == null)
            {
                return;
            }

            ItemInstance item = icon.Item;
            Log($"클릭을 받았습니다. 칸 '{icon.name}' / 곳 {_side} / 창고 이용 중 {StorageSession.IsOpen} / 아이템 {(item == null ? "없음" : item.kind + " x" + item.ItemCount)}");

            if (item == null || item.ItemCount <= 0)
            {
                return;
            }

            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            if (manager == null)
            {
                return;
            }

            // 개인 인벤 : 창고를 열었든 아니든 항상 공용 인벤으로 보냅니다. (재료와 포션 모두)
            if (_side == Side.Personal)
            {
                if (!TryGetSource(icon, manager, out ContainerRef personalSlot))
                {
                    Log($"출발지를 정하지 못했습니다. (내 번호 {manager.LocalActor}, 칸 번호 {icon.slotNum})");
                    return;
                }

                MoveAsync(item, personalSlot, ContainerRef.PackRef).Forget();
                return;
            }

            // 창고를 이용 중(창고 상자에서 연 동안) : 공용 인벤 → 창고 / 창고 → 공용 인벤
            if (StorageSession.IsOpen)
            {
                if (!TryGetSource(icon, manager, out ContainerRef from))
                {
                    return;
                }

                ContainerRef to = _side == Side.Storage ? ContainerRef.PackRef : ContainerRef.StorageRef;
                MoveAsync(item, from, to).Forget();
                return;
            }

            // 창고를 열지 않았을 때 : 공용 인벤의 포션을 내 개인 인벤으로 옮깁니다. 재료는 옮기지 않습니다.
            if (_packPotionToPersonal && _side == Side.Pack && item.kind == InventorySlotKind.Potion && manager.LocalActor > 0)
            {
                MoveAsync(item, ContainerRef.PackRef, ContainerRef.Personal(manager.LocalActor)).Forget();
            }
        }

        // 클릭한 아이콘이 있는 곳을 요청의 출발지로 바꿉니다. 개인 인벤은 칸 번호로 가리킵니다.
        private bool TryGetSource(InventoryIcon icon, InventoryNetworkSyncManager manager, out ContainerRef from)
        {
            switch (_side)
            {
                case Side.Pack:
                    from = ContainerRef.PackRef;
                    return true;

                case Side.Storage:
                    from = ContainerRef.StorageRef;
                    return true;

                default:
                    from = default;
                    if (manager.LocalActor <= 0 || icon.slotNum < 0)
                    {
                        return false;
                    }

                    from = ContainerRef.Personal(manager.LocalActor, icon.slotNum);
                    return true;
            }
        }

        private async UniTaskVoid MoveAsync(ItemInstance item, ContainerRef from, ContainerRef to)
        {
            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            if (manager == null)
            {
                return;
            }

            // 개인 인벤은 칸 번호로 가리키므로 재료 번호, 포션 번호를 보내지 않습니다. (개수만 지정하면 그만큼만 옮김, 0 이면 칸 전체)
            bool slotSource = from.Kind == ContainerKind.Personal;

            InventoryResult result;
            if (item.kind == InventorySlotKind.Potion)
            {
                // 공용 인벤, 창고의 포션은 인스턴스 번호로 가리킵니다.
                result = slotSource
                    ? await manager.TransferAsync(from, to)
                    : await manager.TransferAsync(from, to, instanceId: item.instanceId);
            }
            else
            {
                bool all = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                int count = all ? item.ItemCount : Mathf.Min(_materialClickCount, item.ItemCount);

                result = slotSource
                    ? await manager.TransferAsync(from, to, count: count)
                    : await manager.TransferAsync(from, to, materialId: item.itemId, count: count);
            }

            if (!result.Success)
            {
                // 가득 참, 접근 불가 등. 화면 알림은 UI 담당과 정한 뒤에 붙입니다.
                Debug.LogWarning($"[InventoryTransferRelay]: {from.Kind} -> {to.Kind} 이동 실패 ({result.Reason})");
            }
            else
            {
                Log($"{from.Kind} -> {to.Kind} 이동 성공");
                if (item.kind == InventorySlotKind.Potion)
                    OnPotionTransferred?.Invoke(to.Kind);
            }
        }
        #endregion

        #region Debug
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Log(string message)
        {
            Debug.Log($"[InventoryTransferRelay] : {message}", this);
        }
        #endregion
    }
}
