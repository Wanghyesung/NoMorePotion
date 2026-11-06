/// <summary>
/// 한 판의 기록. 결과창 표시, 랭킹 등록에 사용.
/// </summary>
public readonly struct GameRecord
{
    public readonly GameResult Result;
    /// <summary> 총 플레이 타임(초), TimeManager.TotalElapsed 기준 </summary>
    public readonly float PlayTime;
    /// <summary> 생존 일차 </summary>
    public readonly int DayCount;

    public GameRecord(GameResult result, float playTime, int dayCount)
    {
        Result = result;
        PlayTime = playTime;
        DayCount = dayCount;
    }
}
