namespace Enzeun.Runtime
{
    /// <summary>
    /// 플레이어가 있는 지역의 온도 환경입니다. 체온이 변하는 방향을 정합니다. (SurviveStatusManager 참고)
    /// - Normal : 기본 지역입니다. 체온이 기본값(50%)으로 천천히 돌아옵니다.
    /// - Cold : 추운 지역(예: 빙하지대)입니다. 체온이 계속 내려갑니다.
    /// - Hot : 더운 지역(예: 용암지대)입니다. 체온이 계속 올라갑니다.
    /// 지역에 들어가고 나올 때 그쪽에서 SurviveStatusManager.SetTemperatureEnvironment 로 바꿉니다.
    /// </summary>
    public enum TemperatureEnvironment
    {
        Normal = 0,
        Cold,
        Hot,
    }
}
