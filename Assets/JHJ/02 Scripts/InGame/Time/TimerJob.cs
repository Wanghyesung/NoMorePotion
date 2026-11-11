using System;
using System.Threading;

public class TimerJob : IDisposable
{
    public Action Callback { get; }
    public bool IsCancelled { get; private set; }

    // 취소 토큰 연결. 실행이 끝나거나 취소되면 해제 (토큰이 오래 살아도 job을 붙잡지 않게)
    private CancellationTokenRegistration registration;

    public TimerJob(Action callback)
    {
        Callback = callback;
    }

    /// TimerScheduler 전용. 토큰이 취소되면 이 예약도 취소
    internal void LinkToken(CancellationToken token)
    {
        if (!token.CanBeCanceled) return;
        registration = token.Register(Dispose); // 이미 취소된 토큰이면 즉시 Dispose
    }

    /// TimerScheduler 전용. 실행이 끝났거나 취소된 job을 힙에서 꺼낸 뒤 호출
    internal void ReleaseToken()
    {
        registration.Dispose();
    }

    public void Dispose()
    {
        IsCancelled = true;
    }
}
