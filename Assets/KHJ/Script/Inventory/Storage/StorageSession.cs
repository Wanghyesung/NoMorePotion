using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 창고 이용 중인지 나타냅니다. 창고 상자(StorageChest)와 상호작용해서 창고 패널을 연 동안만 true 입니다.
    /// 공용 인벤 ↔ 창고 이동(InventoryTransferRelay)은 이 값이 true 일 때만 동작합니다. 창고 밖에서 인벤토리를 열었을 때 클릭으로 아이템이 옮겨지지 않게 하기 위함입니다.
    /// </summary>
    public static class StorageSession
    {
        #region State
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// 창고 이용을 시작하거나 끝냅니다. StorageChest 가 부릅니다.
        /// </summary>
        public static void SetOpen(bool open)
        {
            IsOpen = open;
        }

        // 도메인 리로드를 끈 상태로 플레이를 다시 시작해도 이전 플레이의 값이 남지 않게 합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsOpen = false;
        }
        #endregion
    }
}
