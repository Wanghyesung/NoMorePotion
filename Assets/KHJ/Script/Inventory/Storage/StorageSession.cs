using System;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 옆 패널(창고, 시체 상자)을 이용 중인지 나타냅니다. 상자(StorageChest, CorpseChest)와 상호작용해서 패널을 연 동안만 값이 있습니다.
    /// - 창고 : IsOpen. 공용 인벤 ↔ 창고 이동(InventoryTransferRelay)은 이 값이 true 일 때만 동작합니다.
    /// - 시체 상자 : OpenCorpseId. 어느 시체 상자를 열었는지(0 이면 없음)이며 CorpsePanel 이 이 상자의 칸을 보여 줍니다.
    /// 창고 밖에서 인벤토리를 열었을 때 클릭으로 아이템이 옮겨지지 않게 하기 위함입니다.
    /// </summary>
    public static class StorageSession
    {
        #region State
        /// <summary>
        /// 창고를 이용 중인지 여부입니다.
        /// </summary>
        public static bool IsOpen { get; private set; }

        /// <summary>
        /// 지금 열어 둔 시체 상자의 번호입니다. 열어 둔 상자가 없으면 0 입니다.
        /// </summary>
        public static int OpenCorpseId { get; private set; }

        public static bool IsCorpseOpen => OpenCorpseId != 0;

        /// <summary>
        /// 창고든 시체 상자든 무언가를 이용 중인지 여부입니다.
        /// </summary>
        public static bool IsAnyOpen => IsOpen || IsCorpseOpen;

        /// <summary>
        /// 열어 둔 시체 상자가 바뀔 때 새 번호와 함께 발생합니다. (닫히면 0) 시체 상자 패널이 칸을 다시 그리는 데 구독합니다.
        /// </summary>
        public static event Action<int> OnCorpseSessionChanged;
        #endregion

        #region Set
        /// <summary>
        /// 창고 이용을 시작하거나 끝냅니다. StorageChest 가 부릅니다.
        /// </summary>
        public static void SetOpen(bool open)
        {
            IsOpen = open;
        }

        /// <summary>
        /// 열어 둔 시체 상자를 정합니다. 0 이면 닫은 것입니다. CorpseChest 와 CorpsePanel 이 부릅니다.
        /// </summary>
        public static void SetOpenCorpse(int corpseId)
        {
            if (OpenCorpseId == corpseId)
            {
                return;
            }

            OpenCorpseId = corpseId;
            OnCorpseSessionChanged?.Invoke(corpseId);
        }

        /// <summary>
        /// 모든 이용 상태를 끝냅니다. (닫힘 감지를 놓쳐 상태가 남았을 때 바로잡는 용도)
        /// </summary>
        public static void ClearAll()
        {
            IsOpen = false;
            SetOpenCorpse(0);
        }

        // 도메인 리로드를 끈 상태로 플레이를 다시 시작해도 이전 플레이의 값이 남지 않게 합니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsOpen = false;
            OpenCorpseId = 0;
            OnCorpseSessionChanged = null;
        }
        #endregion
    }
}
