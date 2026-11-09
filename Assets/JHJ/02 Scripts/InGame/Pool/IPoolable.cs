/// <summary>
/// 풀에서 꺼낼 때 / 반납할 때 알림을 받는 인터페이스. 필요한 컴포넌트만 구현.
/// 없어도 풀 사용 가능 (그냥 껐다 켜기만 함)
/// - 재사용 시 Awake/Start는 다시 안 불림 → 매번 해야 하는 초기화는 OnGetFromPool에서
/// - 꺼져도 UniTask 반복문은 멈추지 않음 → OnReleaseToPool에서 직접 취소할 것 (코루틴은 자동으로 멈춤)
/// </summary>
public interface IPoolable 
{
    /// <summary>
    /// 켜진 뒤 호출 -> 초기화는 여기서 OnEnable 다음
    /// </summary>
    public void OnGetFromPool();

    /// <summary>
    /// 꺼지기 전 호출 -> 정리는 여기서
    /// </summary>
    public void OnReleaseToPool();
}
