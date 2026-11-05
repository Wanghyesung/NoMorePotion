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

        // 이번 Tick 도중(콜백 안에서) 새로 예약된 작업은 다음 Tick에 실행
        // 막지 않으면 delay 0으로 자기 자신을 다시 예약하는 콜백이 같은 while 안에서 무한 반복됨
        // 새 작업은 DueTime >= now 이고 Seq가 가장 크므로, 맨 앞이 새 작업이면 남은 실행 대상도 전부 새 작업
        long seqLimit = nextSeq;

        while (heap.TryPeek(out TimerJob job, out TimerPriority priority)
               && priority.DueTime <= now
               && priority.Seq < seqLimit)
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


