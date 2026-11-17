using System;
using Cysharp.Threading.Tasks;
using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 재해 장판입니다. (SummonDisasterPotionEffect 가 소환. 블랙홀, 블리자드, 메테오 등) 대상을 끌어당기지 않고 대미지와 상태 이상만 줍니다.
    /// 소환 순간 처음 대미지 → 간격마다 틱 대미지 → 끝날 때 마지막 대미지 → 잠시 뒤 풀에 반납합니다.
    /// 상태 이상은 소환 순간과 틱마다 범위 안의 대상에게 걸립니다. (마지막 대미지 순간에는 걸지 않음)
    ///
    /// 멀티플레이 : 폭발 RPC 를 받은 모든 기기가 각자 이 장판을 소환해서 같은 일정으로 돌립니다. (별도 동기화 없음)
    /// 어떤 대상을 처리할지는 소환할 때 받은 scope 가 정합니다. (내 플레이어는 각 기기, 몬스터와 월드 오브젝트는 방장)
    /// 시간은 UniTask.Delay 로 재서 방장이 아닌 기기에서도 돌아갑니다. (TimeManager.Schedule 은 방장만 실행)
    /// </summary>
    public sealed class DisasterZone : MonoBehaviour
    {
        #region Config
        // 크기를 맞출 이펙트 자식입니다. 비어 있으면 이펙트 크기는 바뀌지 않습니다.
        // 이펙트 안의 ParticleSystem 은 모두 Scaling Mode = Hierarchy 여야 스케일이 적용됩니다.
        [SerializeField]
        private Transform effectRoot;

        // effectRoot 스케일이 1일 때 이펙트의 반지름(m)입니다.
        [SerializeField, Min(0.01f)]
        private float effectBaseRadius = 1f;

        // 마지막 대미지 뒤에 이펙트가 마저 재생될 시간(초)입니다. 끝난 뒤에 풀에 반납합니다.
        [SerializeField, Min(0f)]
        private float endLingerSeconds = 1f;
        #endregion

        #region Run
        /// <summary>
        /// 풀에서 꺼낸 직후 한 번 부릅니다. 이 호출에서 처음 대미지가 바로 들어가고, 이후 일정은 비동기로 돕니다.
        /// </summary>
        /// <param name="effect">대미지, 상태 이상, 시간 설정</param>
        /// <param name="radius">대미지 범위 반지름(m). 원래 포션의 폭발 범위</param>
        /// <param name="scope">이 기기가 처리할 대상 범위</param>
        /// <param name="damageMultiplier">던진 사람의 공격력 배율</param>
        public void Initialize(SummonDisasterPotionEffect effect, float radius, PotionTargetScope scope, float damageMultiplier)
        {
            if (effect == null)
            {
                Debug.LogWarning("[DisasterZone]: 소환 효과가 null 이라 바로 반납합니다.", this);
                Finish();
                return;
            }

            WarnStackableStatus(effect);

            // 풀 재사용 때 이전 크기가 남지 않도록 매번 다시 맞춥니다.
            if (effectRoot != null)
            {
                effectRoot.localScale = Vector3.one * (radius / effectBaseRadius);
            }

            RunAsync(effect, radius, scope, damageMultiplier).Forget();
        }

        private async UniTaskVoid RunAsync(SummonDisasterPotionEffect effect, float radius, PotionTargetScope scope, float damageMultiplier)
        {
            // 반납되거나 파괴되면 취소됩니다. 재사용 중인 오브젝트를 옛 일정이 건드리지 않게 합니다.
            var token = PoolManager.GetReleaseToken(gameObject);
            Vector3 position = transform.position;

            // 처음 대미지 : 소환 순간 한 번. 상태 이상도 이때 처음 걸립니다.
            PotionExplosion.ApplyZonePhase(effect.firstHit, effect.statusEffects, position, radius, scope, damageMultiplier);

            // 틱 대미지 : 간격마다. 지속시간이 끝나는 순간에는 틱 대신 마지막 대미지를 줍니다.
            // 예) 지속 5초, 간격 0.5초 → 틱은 0.5 ~ 4.5초에 9번, 5초에 마지막 대미지.
            float interval = Mathf.Max(0.05f, effect.tickIntervalSeconds);
            float duration = Mathf.Max(0.1f, effect.durationSeconds);
            int tickCount = Mathf.Max(0, Mathf.CeilToInt(duration / interval) - 1);

            for (int i = 0; i < tickCount; i++)
            {
                if (await DelayAsync(interval, token))
                {
                    return;
                }

                // 틱마다 상태 이상을 다시 걸어서(지속시간 갱신) 범위 안에 있는 동안 유지합니다.
                PotionExplosion.ApplyZonePhase(effect.tickHit, effect.statusEffects, position, radius, scope, damageMultiplier);
            }

            // 마지막 틱 이후 남은 시간. (0보다 커야 마지막 대미지가 틱과 겹치지 않음)
            float remaining = duration - tickCount * interval;
            if (await DelayAsync(Mathf.Max(0.01f, remaining), token))
            {
                return;
            }

            // 마지막 대미지에는 상태 이상을 걸지 않습니다. (이미 끝나는 순간)
            PotionExplosion.ApplyZonePhase(effect.finalHit, null, position, radius, scope, damageMultiplier);

            // 마지막 폭발 연출이 보일 시간을 둔 뒤 반납합니다.
            if (await DelayAsync(endLingerSeconds, token))
            {
                return;
            }

            Finish();
        }

        /// <summary>
        /// 지정한 시간만큼 기다립니다. 반납·파괴로 취소되면 true 를 반환합니다.
        /// </summary>
        private static async UniTask<bool> DelayAsync(float seconds, System.Threading.CancellationToken token)
        {
            return await UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: token)
                .SuppressCancellationThrow();
        }
        #endregion

        #region Validate
        /// <summary>
        /// 상태 이상이 비어 있거나 중첩 정책이 Stackable 이면 경고합니다. 틱마다 다시 거므로 Stackable 은 틱마다 쌓입니다. (설정 실수 방지)
        /// </summary>
        private void WarnStackableStatus(SummonDisasterPotionEffect effect)
        {
            var statuses = effect.statusEffects;
            for (int i = 0; i < statuses.Count; i++)
            {
                StatusEffectData data = statuses[i] != null ? statuses[i].statusEffect : null;
                if (data == null)
                {
                    Debug.LogWarning($"[DisasterZone]: 상태 이상 {i}번의 상태 효과가 비어 있습니다.", this);
                    continue;
                }

                if (data.stackPolicy == StackPolicy.Stackable)
                {
                    Debug.LogWarning($"[DisasterZone]: 상태 이상 '{data.statusEffectName}'({data.ID})의 중첩 정책이 Stackable 이라 틱마다 쌓입니다. RefreshDuration 으로 바꾸세요.", this);
                }
            }
        }
        #endregion

        #region Release
        private void Finish()
        {
            // 씬 종료 등으로 PoolManager 가 먼저 사라진 경우
            if (PoolManager.Instance == null)
            {
                return;
            }

            PoolManager.Instance.Release(gameObject);
        }
        #endregion
    }
}
