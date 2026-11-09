using System;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 죽은 플레이어가 남기는 시체 상자의 데이터입니다. 위치와 내용물(개인 인벤토리와 같은 칸 구조)을 가집니다.
    /// 여러 명이 열어 볼 수 있어서 팀 공유 인벤토리와 같은 방식(마스터 검증 → 전원 적용)으로 동기화합니다.
    /// 화면에 보이는 상자 오브젝트는 이 데이터를 보고 만드는 쪽의 몫입니다.
    /// </summary>
    public sealed class CorpseBox
    {
        /// <param name="limitProvider">재료 한 칸의 한도를 알려 주는 함수. 지정하지 않으면 재료 SO 의 값을 사용</param>
        public CorpseBox(int id, Vector3 position, InventorySlot[] slots, Func<int, int> limitProvider = null)
        {
            Id = id;
            Position = position;
            Slots = new PersonalInventory(slots != null ? slots.Length : PersonalInventory.DefaultCapacity, limitProvider);
            Slots.Load(slots);
        }

        /// <summary>
        /// 시체 상자 ID 입니다. 마스터가 부여하며 방 안에서 겹치지 않습니다.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 죽은 위치입니다.
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// 상자의 칸입니다. 개인 인벤토리(PersonalInventory)와 같은 구조입니다.
        /// </summary>
        public PersonalInventory Slots { get; }

        /// <summary>
        /// 비어 있는 상자인지 여부입니다.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < Slots.Capacity; i++)
                {
                    if (!Slots.Slots[i].IsEmpty)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
