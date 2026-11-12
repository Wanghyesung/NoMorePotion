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

    [Header("Gizmo / Preview")]
    // 에디터에서 기즈모와 이펙트 미리보기에 쓸 크기 (런타임에는 실제 포션 크기로 덮어씀)
    [SerializeField] private PotionExplosionRange previewRange = PotionExplosionRange.Small;
    #endregion

    private PotionInstance instance;
    // 포션에 반복 체력 증감(초당 대미지) 효과가 없을 때 쓰는 기본 반복 간격(초)과 횟수
    private const float DefaultAttackDelay = 1f;
    private const int DefaultMaxAttackCount = 10;

    private float nextAttackDelay = DefaultAttackDelay; // 초 단위
    private int AttackCount = 0;
    private int MaxAttackCount = DefaultMaxAttackCount;

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

        // 반복 공격(범위 장판)이면 포션의 초당 대미지 간격과 지속시간으로 반복 간격과 횟수를 정한다. (예: 간격 1초, 지속 10초 → 1초마다 10번)
        // 풀 재사용 때 이전 포션의 값이 남지 않도록 매번 기본값에서 다시 정한다.
        nextAttackDelay = DefaultAttackDelay;
        MaxAttackCount = DefaultMaxAttackCount;

        if (!attackOnce && instance.Data.TryGetHealthTickSchedule(out float tickInterval, out float tickDuration))
        {
            nextAttackDelay = tickInterval;
            MaxAttackCount = Mathf.Max(1, Mathf.CeilToInt(tickDuration / tickInterval));
        }

        // lifetime(직렬화 필드)에 직접 더하면 재사용할 때마다 누적됨 → 지역 변수로 계산
        // 반복 공격(범위 장판)은 지속시간(횟수 × 간격)이 끝나는 즉시 반납해서 파티클도 같이 사라지게 한다. (lifetime 은 한 번짜리 폭발에만 쓴다)
        float releaseDelay = attackOnce ? lifetime : MaxAttackCount * nextAttackDelay;

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


        // 첫 공격은 폭발 전체(상태 효과, 생존 상태, 월드 오브젝트 포함)를 적용하고, 이후 반복은 지금 범위 안에 있는 대상에게 체력 증감(초당 대미지)만 적용한다.
        // 범위를 벗어난 대상은 다음 틱부터 맞지 않는다.
        if (AttackCount == 0)
            PotionExplosion.Explode(instance, transform.position);
        else
            PotionExplosion.ExplodeTick(instance, transform.position);

        if (!attackOnce)
        {
            // 이번 공격까지 센 뒤 총 횟수에 도달하면 끝낸다. (지속 10초, 간격 1초면 0~9초에 10번)
            AttackCount += 1;

            if (AttackCount >= MaxAttackCount)
                return;

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

        RestartEffect();
    }

    // 풀에서 꺼낼 때 이전 색으로 이미 재생된 파티클을 지우고 새 색으로 다시 재생한다.
    // effectRoot에 ParticleSystem이 있으면 그걸로 자식까지 한 번에, 없으면(빈 부모) 자식 파티클을 각각 재시작한다.
    private void RestartEffect()
    {
        if (effectRoot == null)
            return;

        if (effectRoot.TryGetComponent(out ParticleSystem rootParticle))
        {
            rootParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rootParticle.Play(true);
            return;
        }

        foreach (ParticleSystem ps in effectRoot.GetComponentsInChildren<ParticleSystem>())
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(false);
        }
    }

#if UNITY_EDITOR
    // 폭발 판정 범위(PotionExplosionConfig 반지름)를 빨간 원으로 그린다. 콜라이더와 무관.
    private void OnDrawGizmosSelected()
    {
        PotionExplosionConfig config = PotionExplosionConfig.Instance;
        if (config == null)
            return;

        // 플레이 중이면 실제 포션 범위, 아니면 previewRange 범위
        PotionExplosionRange range = (instance != null && instance.Data != null)
            ? instance.Data.potionExplosionRange
            : previewRange;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, config.GetRadius(range));
    }

    // Inspector에서 effectBaseRadius나 previewRange를 바꾸면 이펙트 크기를 바로 미리보기한다.
    private void OnValidate()
    {
        PotionExplosionConfig config = PotionExplosionConfig.Instance;
        if (effectRoot == null || config == null)
            return;

        effectRoot.localScale = Vector3.one * (config.GetRadius(previewRange) / effectBaseRadius);
    }
#endif
    #endregion
}
