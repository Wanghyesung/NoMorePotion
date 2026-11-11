using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using SF = UnityEngine.SerializeField;

namespace Enzeun.Runtime
{

    /// <summary>
    /// 개인 인벤토리 화면입니다. 마스터가 가진 내 개인 인벤토리(PersonalInventory)를 읽어서 칸에 그리기만 합니다.
    /// 아이템 이동, 꺼내기 같은 조작은 이 클래스가 아니라 InventoryNetworkSyncManager 의 요청 함수로 보내세요.
    /// 칸 번호 0~9 가 퀵슬롯(키 1~0)이고, 비어 있어도 칸과 번호는 보입니다.
    /// </summary>
    public class PrivateInventory : ItemInventory
    {
        // 칸 순서대로 PersonalInventory 의 칸 0, 1, 2 ... 에 1:1 로 묶입니다.
        [SF] private List<InventoryIcon> privateInventories;

        // 개인 인벤토리는 처음 아이템을 받을 때 만들어지고 전체 목록을 받으면 객체가 바뀌므로, 직접 들고 있지 않고 도우미를 통해 읽습니다.
        private LocalPersonalInventoryView view;
        private PersonalInventory personalInventory => view != null ? view.Current : null;

        private void Start()
        {
            for (int i = 0; i < privateInventories.Count; i++)
            {
                privateInventories[i].SetSlotNum(i);
                privateInventories[i].isPersonalSlot = true;
            }

            view = new LocalPersonalInventoryView();
            view.OnRebound += RefreshInventory;
            view.OnSlotChanged += RefreshInventory;
            view.StartAsync(this.GetCancellationTokenOnDestroy()).Forget();

            RefreshInventory();
        }

        private void OnDestroy()
        {
            view?.Dispose();
            view = null;
        }

        /// <summary>
        /// 모든 칸을 다시 그립니다.
        /// </summary>
        public void RefreshInventory()
        {
            for (int i = 0; i < privateInventories.Count; i++)
            {
                RefreshInventory(i);
            }
        }

        /// <summary>
        /// 칸 하나를 다시 그립니다. 아직 개인 인벤토리가 없으면(아이템을 받기 전) 빈 칸으로 그립니다.
        /// </summary>
        public void RefreshInventory(int index)
        {
            if (index < 0 || index >= privateInventories.Count)
            {
                return;
            }

            PersonalInventory inventory = personalInventory;
            InventorySlot slot = inventory != null && index < inventory.Capacity ? inventory.Slots[index] : default;

            privateInventories[index].ShowSlot(slot);
        }
    }

}