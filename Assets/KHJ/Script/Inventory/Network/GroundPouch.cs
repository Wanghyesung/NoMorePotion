using System;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 바닥에 떨어진 재료 주머니의 "화면 표시"입니다. 데이터는 인벤토리 상태(GroundDrop)에 있고, 이 컴포넌트는 그 주머니 번호(DropId)에 묶여
    /// 위치에 서 있으면서 줍기를 요청하는 일만 합니다. GroundPouchSpawner 가 만들고 없앱니다.
    ///
    /// 줍기 : RequestPickup() 을 호출하면 마스터에게 줍기를 요청합니다. 플레이어의 줍기 동작(E 키)에서 이 주머니를 찾으면 호출하세요.
    /// 플레이어의 탐지 범위(RangeDetector)가 보는 레이어에 이 오브젝트의 콜라이더가 있어야 찾을 수 있습니다. (트리거도 탐지됨)
    /// 모양은 프리팹의 자식에 자유롭게 넣으면 되고, 개수가 바뀌었을 때 표시를 바꾸려면 CountChanged 를 구독합니다.
    /// </summary>
    public class GroundPouch : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, LabelText("주머니 번호")]
        public int DropId { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("재료 번호")]
        public int MaterialId { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("남은 개수")]
        public int Count { get; private set; }

        /// <summary>
        /// 남은 개수가 바뀔 때(일부만 주움) 새 개수와 함께 발생합니다. 개수 표시를 갱신하는 쪽이 구독합니다.
        /// </summary>
        public event Action<int> CountChanged;

        private bool _isPickingUp;

        /// <summary>
        /// 데이터(GroundDrop)에 묶어 위치를 맞춥니다. 만든 직후 한 번 호출합니다.
        /// </summary>
        public void Bind(GroundDrop drop)
        {
            DropId = drop.Id;
            MaterialId = drop.MaterialId;
            Count = drop.Count;
            transform.position = drop.Position;
            gameObject.name = $"GroundPouch_{drop.Id}";
        }

        /// <summary>
        /// 남은 개수를 새 값으로 갱신합니다.
        /// </summary>
        public void Refresh(GroundDrop drop)
        {
            if (Count == drop.Count)
            {
                return;
            }

            Count = drop.Count;
            CountChanged?.Invoke(Count);
        }

        /// <summary>
        /// 이 주머니를 줍습니다. 동작이 끝나기를 기다리지 않고 요청만 보냅니다. (줍기 애니메이션 이벤트처럼 결과가 필요 없을 때)
        /// </summary>
        public void RequestPickup()
        {
            RequestPickupAsync().Forget();
        }

        /// <summary>
        /// 이 주머니를 줍고 결과를 돌려줍니다. to 를 지정하지 않으면 공용 인벤으로 받습니다.
        /// 이미 요청을 보내고 기다리는 중이면 중복 요청을 보내지 않고 Blocked 로 돌려줍니다.
        /// 성공하면 주머니는 모든 클라이언트에서 사라지고(전부 주운 경우), 일부만 들어갔으면 남은 개수로 갱신됩니다.
        /// 결과가 Full 이면 받을 곳이 가득 찬 것이니 사용자에게 알려 주세요.
        /// </summary>
        public async UniTask<InventoryResult> RequestPickupAsync(ContainerRef to = default)
        {
            if (_isPickingUp)
            {
                return InventoryResult.Fail(InventoryDenyReason.Blocked);
            }

            InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[GroundPouch] : 씬에 InventoryNetworkSyncManager 가 없어 주울 수 없습니다.");
                return InventoryResult.Fail(InventoryDenyReason.NotInRoom);
            }

            _isPickingUp = true;

            try
            {
                return await manager.PickupAsync(DropId, to);
            }
            finally
            {
                // 이 오브젝트가 그 사이 사라졌어도 안전하도록 필드만 되돌립니다.
                _isPickingUp = false;
            }
        }
    }
}
