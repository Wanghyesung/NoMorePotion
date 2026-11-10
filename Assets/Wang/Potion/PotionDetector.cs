using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;

// 범위 진입과 반복 공격 시점만 관리한다. 실제 대상 탐색과 효과는 PotionExplosion에 맡긴다.
public sealed class PotionDetector : MonoBehaviour
{
    [SerializeField] private bool attackOnce = true;
    [SerializeField, Min(0.02f)] private float lifetime = 5f;

    #region 진희진
    [Header("Effect")]
    // 크기를 조절할 이펙트 자식. 비어 있으면 이펙트 크기는 바뀌지 않는다.
    // 이펙트 안의 ParticleSystem은 모두 Scaling Mode = Hierarchy여야 스케일이 적용된다.
    [SerializeField] private Transform effectRoot;
    [SerializeField, Min(0.01f)] private float effectBaseRadius = 1f; // effect scale 1일 때 이펙트 반지름(m)

    [Header("Effect Color")]
    // 포션 색을 넣지 않을 파티클 (Distortion, 검은 파편, 회색 명암용 등)
    [SerializeField] private ParticleSystem[] colorExcludes;

    private ParticleSystem[] colorSystems;
    private Color[] baseColors;
    #endregion

    private PotionInstance instance;
    private float nextAttackDelay = 1f; // 초 단위
    private int AttackCount = 0;
    private int MaxAttackCount = 10;

    private void Awake()
    {
        CacheEffectColors();
    }

    public void Initialize(PotionInstance potionInstance)
    {

        if (potionInstance == null || potionInstance.Data == null)
        {
            Debug.LogError("PotionDetector에 포션 데이터와 폭발 범위 설정이 필요합니다.", this);
            Destroy(gameObject);
            return;
        }

        instance = potionInstance;

        #region 진희진
        // 판정과 이펙트는 같은 radius(소 3 / 중 5 / 대 8m, PotionExplosionConfig) 따로 적용
        // 루트 스케일은 1로 고정, 루트를 키우면 자식 이펙트까지 같이 커져 크기가 이중으로 곱해진다.

        float radius = PotionExplosionConfig.Instance.GetRadius(instance.Data.potionExplosionRange);

        transform.localScale = Vector3.one; // 루트는 그대로

        // 이펙트 스케일 = 목표 반지름 / 기준 반지름  (예: 기준 2m, 대 8m → 4배)
        if (effectRoot != null)
            effectRoot.localScale = Vector3.one * (radius / effectBaseRadius);

        // 이펙트 색 = 원래 색 × 포션 색 (PotionData의 포션 액체 색상)
        ApplyEffectColor(instance.Data.potionColor);

        #endregion

        // 풀 재사용 시 이전 사용분 값이 남아 있으므로 초기화
        AttackCount = 0;

        // lifetime(직렬화 필드)에 직접 더하면 재사용할 때마다 누적됨 → 지역 변수로 계산
        // 반복 공격이면 공격이 다 끝날 때까지 살아 있어야 하므로 공격 시간(횟수 × 간격)만큼 더함
        float releaseDelay = attackOnce ? lifetime : lifetime + MaxAttackCount * nextAttackDelay;

        // 반납 토큰: 먼저 반납되거나 파괴되면 예약 취소 → 재사용 중인 오브젝트를 옛 예약이 반납하지 않음
        TimeManager.Instance.Schedule(releaseDelay, () =>
        {
            if (PoolManager.Instance == null) // 씬 종료 등으로 PoolManager가 먼저 사라진 경우
                return;

            PoolManager.Instance.Release(gameObject);
        }, PoolManager.GetReleaseToken(gameObject));

        TryAttack();
    }

    private void TryAttack()
    {
        if (instance == null)
            return;


        PotionExplosion.Explode(instance, transform.position);

        if (!attackOnce)
        {
            if (AttackCount >= MaxAttackCount)
                return;

            AttackCount += 1;
            WaitNextAttackAsync().Forget();
        }
    }

    private async UniTaskVoid WaitNextAttackAsync()
    {
        // 오브젝트가 꺼져도 UniTask는 멈추지 않음 → 반납 토큰으로 반납·파괴 시 반복 공격 중단
        bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(nextAttackDelay),
                cancellationToken: PoolManager.GetReleaseToken(gameObject))
            .SuppressCancellationThrow();
        if (cancelled)
            return;

        TryAttack();
    }

    #region 진희진

    // 이펙트 파티클과 원래 Start Color를 한 번만 저장한다.
    // 풀 재사용 때 현재 색에 다시 곱하면 점점 어두워지므로, 항상 원래 색 × 포션 색으로 계산하기 위함.
    private void CacheEffectColors()
    {
        if (effectRoot == null)
            return;

        var list = new System.Collections.Generic.List<ParticleSystem>();
        foreach (ParticleSystem ps in effectRoot.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (colorExcludes == null || Array.IndexOf(colorExcludes, ps) < 0)
                list.Add(ps);
        }

        colorSystems = list.ToArray();
        baseColors = new Color[colorSystems.Length];
        for (int i = 0; i < colorSystems.Length; i++)
            baseColors[i] = colorSystems[i].main.startColor.color;
    }

    // 흰색 베이스의 원래 색에 포션 색을 곱한다. 명암은 원래 색이, 색조는 포션 색이 정한다. 투명도는 원래 값 유지.
    private void ApplyEffectColor(Color potionColor)
    {
        if (colorSystems == null)
            return;

        for (int i = 0; i < colorSystems.Length; i++)
        {
            Color c = baseColors[i] * potionColor;
            c.a = baseColors[i].a;
            ParticleSystem.MainModule main = colorSystems[i].main;
            main.startColor = c;
        }

        // 풀에서 꺼낼 때 이전 색으로 이미 재생된 파티클을 지우고 새 색으로 다시 재생한다.
        if (effectRoot.TryGetComponent(out ParticleSystem rootParticle))
        {
            rootParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rootParticle.Play(true);
        }
    }
    #endregion
}
