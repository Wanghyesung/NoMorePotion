using System;
using UnityEngine;

public class TimerScheduler
{
    private MinHeap<TimerJob, TimerPriority> heap = new MinHeap<TimerJob, TimerPriority>();
    private long nextSeq;


    public TimerJob Schedule(float delay, Action callback)
    {
        if(callback == null)
            throw new ArgumentNullException(nameof(callback));


        if (delay < 0)
            delay = 0;

        TimerJob job = new TimerJob(callback);

        TimerPriority priority = new TimerPriority { DueTime = Time.time + delay, Seq = nextSeq };
        nextSeq++;

        heap.Enqueue(job, priority);

        return job;
    }

    public void Tick()
    {
        float now = Time.time;

        while (heap.TryPeek(out TimerJob job, out TimerPriority priority) && priority.DueTime <= now)
        {

            heap.Dequeue();

            if (job.IsCancelled) continue;

            try
            {
                job.Callback();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}


