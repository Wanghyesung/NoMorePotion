using System;
using UnityEngine;

public class TimerJob : IDisposable
{
    public Action Callback { get; }
    public bool IsCancelled { get; private set; }

    public TimerJob(Action callback)
    {
        Callback = callback;
    }


    public void Dispose()
    {
        IsCancelled = true;
    }
}
