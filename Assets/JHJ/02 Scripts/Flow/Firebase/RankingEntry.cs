using Firebase.Database;
using System.Collections.Generic;

public class RankingEntry
{
    public const string KEY_UID = "uid";
    public const string KEY_MEMBERS = "members";
    public const string KEY_MEMBER_COUNT = "memberCount";
    public const string KEY_PLAY_TIME = "playTime";    // 규칙의 .indexOn과 같아야 함
    public const string KEY_DAY_COUNT = "dayCount";
    public const string KEY_CLEARED_AT = "clearedAt";

    public string Uid;
    public List<string> Members;
    public int MemberCount;
    public double PlayTime;
    public int DayCount;

    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { KEY_UID,         Uid },
            { KEY_MEMBERS,     new List<object>(Members) },   // List<string> → List<object>
            { KEY_MEMBER_COUNT, MemberCount },
            { KEY_PLAY_TIME,    PlayTime },
            { KEY_DAY_COUNT,    DayCount },
            { KEY_CLEARED_AT,   ServerValue.Timestamp },       // 서버가 저장 순간의 시각으로 채움
        };
    }
}
