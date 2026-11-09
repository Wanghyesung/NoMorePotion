using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 바닥에 떨어진 재료 주머니 한 개입니다. 한 종류의 재료가 Count 개 들어 있고, 줍기는 주머니 단위입니다.
    /// 위치는 마스터가 정해서 모든 클라이언트에 같은 값으로 알리므로, 화면의 주머니 오브젝트는 이 위치에 만들면 됩니다.
    /// </summary>
    public sealed class GroundDrop
    {
        public GroundDrop(int id, int materialId, int count, Vector3 position)
        {
            Id = id;
            MaterialId = materialId;
            Count = count;
            Position = position;
        }

        /// <summary>
        /// 주머니 ID 입니다. 마스터가 부여하며 방 안에서 겹치지 않습니다.
        /// </summary>
        public int Id { get; }

        public int MaterialId { get; }

        /// <summary>
        /// 주머니에 남은 재료 개수입니다. 공용 인벤이 가득 차서 일부만 주웠으면 남은 만큼이 됩니다.
        /// </summary>
        public int Count { get; set; }

        public Vector3 Position { get; }
    }

    /// <summary>
    /// 공용 인벤(배낭)과 공용 창고처럼 "재료 종류별 개수 + 포션 목록" 구조의 보관 장소 한 곳입니다.
    /// </summary>
    public sealed class ItemStore
    {
        public ItemStore(MaterialInventory materials, PotionInventory potions)
        {
            Materials = materials;
            Potions = potions;
        }

        public MaterialInventory Materials { get; }

        public PotionInventory Potions { get; }
    }
}
