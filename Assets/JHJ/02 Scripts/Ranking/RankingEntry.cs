using Firebase.Database;
using System;
using System.Collections.Generic;
using UnityEngine;

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

    public static RankingEntry FromSnapshot(DataSnapshot snapshot)
    {
        try
        {
            List<string> members = new List<string>();

            foreach (DataSnapshot child in snapshot.Child(KEY_MEMBERS).Children)
            {
                if (child.Value is string name) members.Add(name);
            }

            return new RankingEntry
            {
                Uid = snapshot.Child(KEY_UID).Value as string,
                Members = members,
                MemberCount = Convert.ToInt32(snapshot.Child(KEY_MEMBER_COUNT).Value),
                PlayTime = Convert.ToDouble(snapshot.Child(KEY_PLAY_TIME).Value),
                DayCount = Convert.ToInt32(snapshot.Child(KEY_DAY_COUNT).Value)
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RankingEntry] 읽기 실패 {snapshot.Key}: {e.Message}");
            return null;
        }
    }
}
