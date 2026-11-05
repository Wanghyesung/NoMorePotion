using System;


namespace Enzeun.SO
{
    /// <summary>
    /// 포션이 가진 효과의 부모 클래스입니다. 포션 SO 는 이 효과들을 리스트로 조합해서 효과를 표현합니다.
    /// 효과 종류는 Odin 타입 드롭다운으로 고릅니다. (효과마다 ID 를 두지 않고 SO 를 직접 참조)
    /// </summary>
    [Serializable]
    public abstract class PotionEffect
    {
    }
}
