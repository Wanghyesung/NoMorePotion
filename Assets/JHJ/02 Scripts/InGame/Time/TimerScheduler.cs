using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// 시각 기준 예약 실행. 시각은 호출하는 쪽이 넘겨준다 (스케줄러는 시간 출처를 모름)
/// </summary>
/// <remarks>
/// TimeManager가 게임 시간(TotalElapsed)을 넘겨준다.
/// Time.time(앱 실행 시간)을 쓰지 않는 이유
///   - 시계가 멈춘 동안에도 Time.time은 흘러서, 다시 돌면 밀린 예약이 한꺼번에 실행됨
///   - 불러오기 후 Time.time은 0부터 다시 시작해서, 저장한 예약 시각과 맞지 않음
/// </remarks>
public class TimerScheduler
{
    private MinHeap<TimerJob, TimerPriority> heap = new MinHeap<TimerJob, TimerPriority>();
    private long nextSeq;


    /// <param name="now">현재 시각 (Tick에 넘기는 시각과 같은 기준)</param>
    public TimerJob Schedule(float now, float delay, Action callback)
    {
        return Schedule(now, delay, callback, CancellationToken.None);
    }

    /// <param name="now">현재 시각 (Tick에 넘기는 시각과 같은 기준)</param>
    /// <param name="cancellationToken">취소되면 이 예약도 취소 (예약 시점에 이미 취소됐으면 실행 안 됨)</param>
    public TimerJob Schedule(float now, float delay, Action callback, CancellationToken cancellationToken)
    {
        if(callback == null)
            throw new ArgumentNullException(nameof(callback));


        if (delay < 0)
            delay = 0;

        TimerJob job = new TimerJob(callback);

        TimerPriority priority = new TimerPriority { DueTime = now + delay, Seq = nextSeq };
        nextSeq++;

        heap.Enqueue(job, priority);
        job.LinkToken(cancellationToken);

        return job;
    }

    /// <param name="now">현재 시각. 이 시각까지 도래한 예약을 실행</param>
    public void Tick(float now)
    {
        // 이번 Tick 도중(콜백 안에서) 새로 예약된 작업은 다음 Tick에 실행
        // 막지 않으면 delay 0으로 자기 자신을 다시 예약하는 콜백이 같은 while 안에서 무한 반복됨
        // 새 작업은 DueTime >= now 이고 Seq가 가장 크므로, 맨 앞이 새 작업이면 남은 실행 대상도 전부 새 작업
        long seqLimit = nextSeq;

        while (heap.TryPeek(out TimerJob job, out TimerPriority priority)
               && priority.DueTime <= now
               && priority.Seq < seqLimit)
        {

            heap.Dequeue();
            job.ReleaseToken(); // 힙에서 빠졌으니 토큰 연결 해제

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


