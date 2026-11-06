using Enzeun.SO;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드에 떨어져 있는 재료 한 묶음(재료 + 개수)입니다. 월드 오브젝트가 소진되면 드롭 결과로 만들어집니다.
    /// 포션이 PotionInstance 로 "데이터를 담은 런타임 개체"를 갖는 것처럼, 재료도 이 컴포넌트가 재료 데이터와 개수를 담아 월드에 존재합니다.
    /// 인벤토리 안에서는 개수만 다루므로(InventorySlot / MaterialInventory) 별도 인스턴스 클래스는 두지 않고, 월드에 있는 동안만 이 컴포넌트를 씁니다.
    ///
    /// 네트워크로 만들 때는 재료 ID 와 개수 두 값만 보내고, 받는 쪽이 Initialize(재료 ID, 개수)로 복원하면 됩니다.
    /// </summary>
    public class MaterialItem : MonoBehaviour
    {
        [SerializeField, LabelText("재료 데이터"), Required("재료 데이터를 연결해 주세요.")]
        private MaterialData _data;
        public MaterialData Data => _data;

        [SerializeField, LabelText("개수"), MinValue(1)]
        private int _count = 1;
        public int Count => _count;

        /// <summary>
        /// 재료 데이터와 개수를 지정합니다. 개수는 1 이상이어야 하며, 그보다 작으면 1 로 보정합니다.
        /// </summary>
        public void Initialize(MaterialData data, int count)
        {
            _data = data;
            _count = Mathf.Max(1, count);
        }

        /// <summary>
        /// 재료 ID 와 개수로 지정합니다. 네트워크로 받은 값을 복원할 때 사용합니다.
        /// </summary>
        /// <returns>모르는 재료 ID 이면 false (지정하지 않음)</returns>
        public bool Initialize(int materialId, int count)
        {
            if (PotionDataManager.Instance == null || !PotionDataManager.Instance.TryGetMaterialData(materialId, out MaterialData data))
            {
                Debug.LogWarning($"[MaterialItem]: 알 수 없는 재료 ID({materialId}) 입니다.");
                return false;
            }

            Initialize(data, count);
            return true;
        }

        /// <summary>
        /// 이 재료 묶음을 인벤토리 칸 값으로 바꿉니다. 줍는 쪽이 개인 인벤토리에 넣을 때 사용합니다.
        /// </summary>
        public InventorySlot ToSlot()
        {
            return _data != null ? InventorySlot.OfMaterial(_data.materialID, _count) : default;
        }
    }
}
