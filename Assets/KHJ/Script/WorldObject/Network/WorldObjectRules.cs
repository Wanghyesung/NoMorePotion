using System.Collections.Generic;
using Enzeun.SO;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트가 맞은 "타격" 한 번의 종류입니다.
    /// </summary>
    public enum WorldObjectHitKind
    {
        None,
        Interact,       // 플레이어의 상호작용
        PotionDamage,   // 포션의 월드 오브젝트 대미지 효과
    }

    /// <summary>
    /// 월드 오브젝트의 횟수 차감 규칙을 한 곳에 모은 정적 도우미입니다. (네트워크, Unity 오브젝트를 모르는 순수 규칙)
    /// 혼자 할 때(WorldObject)와 멀티플레이에서 마스터가 검증할 때(WorldObjectNetState) 같은 규칙을 쓰도록 하나로 둡니다.
    ///
    /// 규칙 (기획)
    /// - 상호작용 타입(Interacting) : 상호작용 1회당 1 차감, 포션 대미지는 받지 않음
    /// - 파괴 타입(Destroying) : 상호작용으로는 차감되지 않고, 대상 종류가 맞는 포션 대미지만 대미지 수치만큼 차감
    /// </summary>
    public static class WorldObjectRules
    {
        /// <summary>
        /// 이 타격으로 줄어드는 횟수를 계산합니다. 0 이면 아무 효과가 없습니다.
        /// </summary>
        /// <param name="targetMask">포션 대미지의 대상 종류 비트 마스크 (WorldObjectType 값 번째 비트). 상호작용에서는 쓰지 않습니다.</param>
        public static int ComputeDecrease(WorldObjectData data, WorldObjectHitKind kind, int damage, int targetMask)
        {
            if (data == null)
            {
                return 0;
            }

            switch (kind)
            {
                case WorldObjectHitKind.Interact:
                    return data.worldObjectDropType == WorldObjectDropType.Interacting ? 1 : 0;

                case WorldObjectHitKind.PotionDamage:
                    if (data.worldObjectDropType != WorldObjectDropType.Destroying || damage <= 0)
                    {
                        return 0;
                    }

                    return (targetMask & (1 << (int)data.worldObjectType)) != 0 ? damage : 0;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 포션 효과의 대상 종류 목록을 비트 마스크로 바꿉니다. (WorldObjectType 은 7 가지라 byte 하나에 들어갑니다.)
        /// </summary>
        public static int ToTargetMask(IReadOnlyList<WorldObjectType> targetTypes)
        {
            int mask = 0;

            if (targetTypes == null)
            {
                return mask;
            }

            for (int i = 0; i < targetTypes.Count; i++)
            {
                mask |= 1 << (int)targetTypes[i];
            }

            return mask;
        }
    }
}
