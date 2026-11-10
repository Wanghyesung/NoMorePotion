using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SF = UnityEngine.SerializeField;

public class MonsterHPHud : MonoBehaviour
{
    [SF] private MonsterCombat combat;

    [SF] private Canvas healthCanvas;
    [SF] private CanvasGroup canvasGroup;

    [Header("체력바 수치 관련")]
    [SF] private Image healthBar;
    [SF] private Image chipBar;
    [SF] private TextMeshProUGUI nameText;
    [SF] private TextMeshProUGUI nowHealthText;
    [SF] private TextMeshProUGUI maxHealthText;

    [Header("잔상바 수치 관련")]
    [SF] private float chipDelay = 0.4f;
    [SF] private float chipDuration = 0.25f;
    [SF] private Ease chipEase = Ease.OutQuad;
    [SF] private float healDuration = 0.2f;

    [Header("허드 표시 관련")]
    [Tooltip("체력이 가득 차 있으면 숨김, 피격 후 showDuration 동안만 표시 (엘리트·보스는 끄기)")]
    [SF] private bool hideIsFull = true;
    [SF] private float showDuration = 3f;
    [SF] private float fadeDuration = 0.2f;
    // 
    [SF] private bool faceCamera = true;

    private Camera cam;
    private float maxHealth = 1f;
    private float currentHealth = 1f;

    private Tween hpTween;
    private Tween chipTween;
    private Tween fadeTween;
    private CancellationTokenSource hideCts;

    public float Ratio => currentHealth / maxHealth;

    private void Awake()
    {
        if (combat == null)
            combat = GetComponentInParent<MonsterCombat>();
    }
    private void Start()
    {
        cam = Camera.main;
        healthCanvas.worldCamera = cam;
    }

    private void LateUpdate()
    {
        if (faceCamera && cam != null)
            healthCanvas.transform.rotation = cam.transform.rotation;
    }

    /// <summary>
    /// 스폰 / 풀에서 꺼낼 때 호출하는 초기화 함수
    /// </summary>
    public void Init(float max, float current = -1f, string name = null)
    {
        maxHealth = Mathf.Max(1f, max);
        currentHealth = current < 0f ? maxHealth : Mathf.Clamp(current, 0f, maxHealth);
        if (nameText != null && name != null) nameText.SetText(name);
        KillTweens();
        CancelHide();

        healthBar.fillAmount = Ratio;
        chipBar.fillAmount = Ratio;
        UpdateTexts();

        bool visible = !(hideIsFull && Ratio >= 1f) && currentHealth > 0f;
        canvasGroup.alpha = visible ? 1f : 0f;
    }

    public void HealthChange(int before, int now) => SetHealth(now);

    public void SetHealth(float newHealth)
    {
        newHealth = Mathf.Clamp(newHealth, 0f, maxHealth);
        if (Mathf.Approximately(newHealth, currentHealth)) return;

        bool damaged = newHealth < currentHealth;
        currentHealth = newHealth;
        float ratio = Ratio;

        UpdateTexts();

        // 피격시
        if (damaged)
        {
            hpTween?.Kill();
            healthBar.fillAmount = ratio;

            chipTween?.Kill();
            chipTween = chipBar.DOFillAmount(ratio, chipDuration)
                .SetDelay(chipDelay)
                .SetEase(chipEase)
                .SetLink(gameObject);
        }
        // 체력 회복 시
        else
        {
            chipTween?.Kill();
            chipBar.fillAmount = ratio;

            hpTween?.Kill();
            hpTween = healthBar.DOFillAmount(ratio, healDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        // 사망시
        if (currentHealth <= 0f)
        {
            CancelHide();
            FadeTo(0f, chipDelay + chipDuration);
            return;
        }

        // 체력이 전부 채워져있는 경우
        if (hideIsFull)
        {
            if (damaged || canvasGroup.alpha > 0f) FadeTo(1f);
            ScheduleHide();
        }
        else
        {
            FadeTo(1f);
        }
    }

    /// <summary>
    /// 체력 텍스트 업데이트 함수
    /// </summary>
    private void UpdateTexts()
    {
        if (nowHealthText != null) 
            nowHealthText.SetText("{0:0}", Mathf.Ceil(currentHealth));

        if (maxHealthText != null) 
            maxHealthText.SetText("{0:0}", Mathf.Ceil(maxHealth));
    }

    /// <summary>
    /// 캔버스 Fade 함수
    /// </summary>
    /// <param name="target"></param>
    /// <param name="delay"></param>
    public void FadeTo(float target, float delay = 0f)
    {
        fadeTween?.Kill();
        if (Mathf.Approximately(canvasGroup.alpha, target) && delay <= 0f) return;

        fadeTween = canvasGroup.DOFade(target, fadeDuration)
            .SetDelay(delay)
            .SetLink(gameObject);
    }

    /// <summary>
    /// 숨김 지연 중 피격시 숨김 지연 중인 딜레이 함수를 취소시키고 재시작 하는 함수
    /// (첫시작도 동일)
    /// </summary>
    private void ScheduleHide()
    {
        CancelHide();
        hideCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        HideAfterDelay(hideCts.Token).Forget();
    }

    /// <summary>
    /// 설정 시간 이후 허드를 다시 감추는 지연 함수
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async UniTaskVoid HideAfterDelay(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(showDuration), cancellationToken: token)
            .SuppressCancellationThrow();
        if (canceled) return;

        FadeTo(0f);
    }

    /// <summary>
    /// 숨김에 사용된 Unitask 취소 함수
    /// </summary>
    private void CancelHide()
    {
        if (hideCts == null) return;
        hideCts.Cancel();
        hideCts.Dispose();
        hideCts = null;
    }

    /// <summary>
    /// 작동중인 트윈 전부 Kill
    /// </summary>
    private void KillTweens()
    {
        hpTween?.Kill();
        chipTween?.Kill();
        fadeTween?.Kill();
    }

    private void OnDisable()
    {
        CancelHide();
        KillTweens();
    }
}
