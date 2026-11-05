using System;


public struct TimerPriority : IComparable<TimerPriority>
{
    public float DueTime;
    public long Seq;


    public int CompareTo(TimerPriority other)
    {
        int timeCompare = DueTime.CompareTo(other.DueTime);
        if (timeCompare != 0)
            return timeCompare;

        return Seq.CompareTo(other.Seq);
    }
}
