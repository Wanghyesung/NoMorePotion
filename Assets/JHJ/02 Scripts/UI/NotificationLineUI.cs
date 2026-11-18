using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using SF = UnityEngine.SerializeField;


/// <summary>
/// 알림창의 알림 한 줄. 나타난 뒤 일정 시간이 지나면 사라지고, 끝나면 OnFinished로 패널에 알림
/// - 풀에서 재사용되므로 매번 Play로 초기화
/// - 비활성화/반납은 직접 하지 않음 (NotificationUI가 처리)
/// </summary>
public class NotificationLineUI : MonoBehaviour
{
    [SF] private CanvasGroup group;
    [SF] private TMP_Text text;
    [SF] private float showSeconds = 3f;
    [SF] private float fadeInSeconds = 0.15f;
    [SF] private float fadeOutSeconds = 0.3f;

    /// <summary>
    /// 표시가 끝났을 때 호출 (정상 종료, 강제 종료 모두 한 번만). 패널이 반납 함수를 연결
    /// </summary>
    public event Action<NotificationLineUI> OnFinished;
    private CancellationTokenSource cts;
    private Tween fade;

    //======================================
    // 패널 호출

    /// <summary>
    /// 알림 표시 시작. 이전 상태를 정리하고 페이드 인 후 수명 타이머 시작
    /// </summary>
    public void Play(string msg)
    {
        Stop();
        text.SetText(msg);
        group.alpha = 0;
        fade = group.DOFade(1, fadeInSeconds).SetUpdate(true).SetLink(gameObject);

        cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        LifeAsync(cts.Token).Forget();
    }

    /// <summary>
    /// 최대 개수 초과로 밀려날 때 호출. 남은 시간과 상관없이 즉시 종료
    /// </summary>
    public void ForceEnd()
    {
        Stop();
        OnFinished?.Invoke(this);
    }

    //======================================
    // 수명

    /// <summary>
    /// 줄의 수명. showSeconds 동안 표시 → 페이드 아웃 → OnFinished 호출
    /// 중간에 취소되면(ForceEnd, 비활성화, 파괴) 아무것도 하지 않고 종료
    /// </summary>
    private async UniTaskVoid LifeAsync(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(showSeconds),
            ignoreTimeScale: true,
            cancellationToken: token).SuppressCancellationThrow();

        if (canceled)
            return;

        fade?.Kill();
        fade = group.DOFade(0, fadeOutSeconds).SetUpdate(true).SetLink(gameObject);

        canceled = await fade.ToUniTask(cancellationToken: token).SuppressCancellationThrow();

        if (canceled)
            return;

        OnFinished?.Invoke(this);
    }

    // ======================================
    // 정리

    /// <summary>
    /// 진행 중인 수명 타이머와 트윈 정리
    /// </summary>
    private void Stop()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;

        fade?.Kill();
    }

    /// <summary>
    /// 꺼져도 UniTask는 멈추지 않으므로 비활성화 시 정리
    /// </summary>
    private void OnDisable()
    {
        Stop();
    }
}
