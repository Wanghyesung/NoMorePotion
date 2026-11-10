using UnityEngine;

public static class TimeFormat
{
    /// <summary>
    /// 플레이타임(초)을 "시:분:초" (00:00:00)로 표시. 음수는 0으로 처리. 결과창, 랭킹 창에서 사용
    /// </summary>
    public static string ToPlayTime(double seconds)
    {
        int total = seconds < 0 ? 0 : (int)seconds;

        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;

        return $"{h:00}:{m:00}:{s:00}";
    }
}
