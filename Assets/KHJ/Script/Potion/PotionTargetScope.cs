using System;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 포션 폭발이 효과를 적용할 대상의 범위입니다. 멀티플레이에서 "누가 어떤 대상을 맡는가"를 나눌 때 사용합니다.
    /// 원칙은 "상태의 주인이 적용한다"입니다.
    /// - 플레이어는 체력, 버프, 생존 상태를 각 클라이언트가 가지므로 각자 자기 플레이어만 처리합니다. (LocalPlayer)
    /// - 몬스터, 월드 오브젝트처럼 플레이어가 아닌 대상은 방장이 한 곳에서만 처리합니다. (NonPlayers)
    /// 남의 플레이어는 어떤 범위에서도 처리하지 않습니다.
    /// </summary>
    [Flags]
    public enum PotionTargetScope
    {
        None = 0,

        /// <summary>이 기기의 내 플레이어</summary>
        LocalPlayer = 1,

        /// <summary>플레이어가 아닌 모든 대상 (몬스터, 수정구슬, 월드 오브젝트 등)</summary>
        NonPlayers = 2,

        All = LocalPlayer | NonPlayers,
    }
}
