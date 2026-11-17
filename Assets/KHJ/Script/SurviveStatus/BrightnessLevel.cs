namespace Enzeun.Runtime
{
    /// <summary>
    /// 밝기 환경 단계입니다. 값은 시트의 "밝기 N단계"의 N 과 같습니다.
    /// 플레이어가 있는 곳의 밝기에 따라 정신력이 변하는 방식이 달라집니다. (SurviveStatusManager 참고)
    /// - Bright (3단계) : 기본 환경입니다. 정신력이 50% 이하이면 50% 까지 천천히 회복합니다.
    /// - Dim (2단계) : 어두운 곳입니다. 정신력이 천천히 감소합니다.
    /// - Dark (1단계) : 매우 어두운 곳입니다. 정신력이 더 빠르게 감소합니다.
    /// 동굴 같은 구역에 들어가면 그쪽에서 SurviveStatusManager.SetBrightness 로 단계를 바꿉니다.
    /// </summary>
    public enum BrightnessLevel
    {
        Dark = 1,
        Dim = 2,
        Bright = 3,
    }
}
