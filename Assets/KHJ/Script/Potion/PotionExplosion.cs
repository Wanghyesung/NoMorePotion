using System.Collections.Generic;
using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 포션 폭발을 일으키는 정적 도우미입니다. 지정한 위치에서 범위 안의 모든 대상에게 포션 효과를 적용합니다.
    ///
    /// - 던지기 : 포션이 부딪힌 위치에서 호출합니다. (Potion 컴포넌트)
    /// - 마시기 : 플레이어의 위치에서 호출합니다. 범위에 플레이어 자신도 포함되므로 자신에게도 적용됩니다.
    ///
    /// 한 대상에 콜라이더가 여러 개여도 효과는 대상당 한 번만 적용합니다.
    ///
    /// 멀티플레이 : 모든 기기가 같은 폭발을 각자 부르되, 어떤 대상을 처리할지는 scope(PotionTargetScope)로 나눕니다.
    /// 내 플레이어는 각 기기가, 플레이어가 아닌 대상(몬스터, 월드 오브젝트)은 방장만 처리하고, 남의 플레이어는 처리하지 않습니다.
    /// scope 를 지정하지 않으면 All(내 플레이어 + 플레이어가 아닌 대상)이라 오프라인이나 단독 호출에서는 이전과 같습니다.
    /// </summary>
    public static class PotionExplosion
    {
        // 한 번의 폭발이 동시에 찾을 수 있는 콜라이더 수입니다. 가득 차면 경고합니다.
        private const int MaxColliders = 64;

        // 매 폭발마다 새로 할당하지 않도록 재사용하는 버퍼입니다.
        private static readonly Collider[] ColliderBuffer = new Collider[MaxColliders];
        private static readonly HashSet<Object> AppliedTargets = new HashSet<Object>();

        /// <summary>
        /// position 에서 폭발을 일으키고, 범위에 닿은 대상 수를 반환합니다.
        /// </summary>
        public static int Explode(PotionData potion, Vector3 position)
        {
            return ExplodeCore(potion, null, position);
        }

        /// <summary>
        /// 제작된 포션 인스턴스를 position 에서 폭발시킵니다. 함정 포션이면 원래 포션의 효과에 함정 키워드의 효과가 더해지고,
        /// 폭발 범위는 원래 포션을 따릅니다.
        /// </summary>
        public static int Explode(PotionInstance instance, Vector3 position, PotionTargetScope scope = PotionTargetScope.All)
        {
            if (instance == null)
            {
                Debug.LogWarning("[PotionExplosion]: 폭발시킬 포션 인스턴스가 null 입니다.");
                return 0;
            }

            return ExplodeCore(instance.Data, instance.TrapKeyword, position, false, scope);
        }

        /// <summary>
        /// 범위 장판이 간격마다 반복하는 틱입니다. 지금 범위 안에 있는 대상에게만 체력 증감(초당 대미지 등) 효과를 적용합니다.
        /// 처음 폭발(Explode)에서 이미 적용한 상태 효과, 생존 상태 증감, 월드 오브젝트 대미지는 반복하지 않고, 화면 연출도 다시 재생하지 않습니다.
        /// </summary>
        public static int ExplodeTick(PotionInstance instance, Vector3 position, PotionTargetScope scope = PotionTargetScope.All)
        {
            if (instance == null)
            {
                return 0;
            }

            return ExplodeCore(instance.Data, instance.TrapKeyword, position, true, scope);
        }

        /// <summary>
        /// 내 플레이어에게만 적용하는 폭발입니다. (scope 가 LocalPlayer 인 Explode/ExplodeTick 과 같음)
        /// </summary>
        /// <param name="potion">효과와 폭발 범위의 기준이 되는 원래 포션</param>
        /// <param name="keyword">함정 키워드. 있으면 그 효과도 같이 적용합니다. (범위는 원래 포션을 따름)</param>
        /// <param name="tickOnly">true 면 체력 증감 효과만 적용하고 연출은 재생하지 않습니다. (범위 장판의 반복 틱)</param>
        public static int ExplodeLocalPlayer(PotionData potion, TrapPotionKeyword keyword, Vector3 position, bool tickOnly)
        {
            return ExplodeCore(potion, keyword, position, tickOnly, PotionTargetScope.LocalPlayer);
        }

        private static int ExplodeCore(PotionData potion, TrapPotionKeyword trapKeyword, Vector3 position, bool tickOnly = false, PotionTargetScope scope = PotionTargetScope.All)
        {
            if (potion == null)
            {
                Debug.LogWarning("[PotionExplosion]: 폭발시킬 포션 데이터가 null 입니다.");
                return 0;
            }

            PotionExplosionConfig config = PotionExplosionConfig.Instance;
            if (config == null)
            {
                Debug.LogWarning($"[PotionExplosion]: 폭발 설정 에셋이 없습니다. (Resources/{PotionExplosionConfig.ResourcePath})");
                return 0;
            }

            // 화면 단위 연출(카메라, 후처리 등)은 씬의 허브가 맡습니다. 허브가 없으면 아무 일도 일어나지 않습니다.
            if (!tickOnly)
            {
                ExplosionFeedbackHub.Play(position, potion.potionSize);
            }

            float radius = config.GetRadius(potion.potionExplosionRange);
            int hitCount = Physics.OverlapSphereNonAlloc(position, radius, ColliderBuffer, config.TargetLayers, QueryTriggerInteraction.Collide);

            if (hitCount >= MaxColliders)
            {
                Debug.LogWarning($"[PotionExplosion]: 폭발 범위 안의 콜라이더가 {MaxColliders}개 이상이라 일부가 누락될 수 있습니다.");
            }

            AppliedTargets.Clear();
            int targetCount = 0;

            // 남의 플레이어 몸 연출에 쓸 피해 수치입니다. (효과 적용과 무관한 연출 전용)
            int visualDamage = GetDamageAmount(potion, trapKeyword);

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = ColliderBuffer[i];
                ColliderBuffer[i] = null;   // 파괴된 오브젝트를 붙잡지 않도록 비웁니다.

                // 컨테이너는 Owner 로 찾습니다. (플레이어 컨테이너는 콜라이더의 부모 계층에 없을 수 있음)
                BuffContainer container = BuffContainer.FindByOwner(collider.transform);

                var target = new PotionTarget
                {
                    BuffContainer = container,
                    Damageable = collider.GetComponentInParent<IDamageable>(),
                    WorldObject = collider.GetComponentInParent<WorldObject>(),
                    IsLocalPlayer = IsLocalPlayerContainer(container),
                };

                // 범위 구분 : 플레이어는 내 플레이어만(남의 플레이어는 그 기기가 처리), 플레이어가 아닌 대상은 NonPlayers 일 때만 처리합니다.
                if (target.Damageable is Player player)
                {
                    if (!player.IsLocalPlayer)
                    {
                        // 남의 플레이어에게는 효과를 적용하지 않습니다. 대신 이 기기에서 보이는 대로 몸 연출만 근사해서 재생합니다.
                        if (visualDamage > 0 && AppliedTargets.Add(player))
                        {
                            PlayRemoteHitFeedback(player, visualDamage);
                        }

                        continue;
                    }

                    if ((scope & PotionTargetScope.LocalPlayer) == 0)
                        continue;
                }
                else if ((scope & PotionTargetScope.NonPlayers) == 0)
                {
                    continue;
                }

                Object key = GetTargetKey(target);
                if (key == null || !AppliedTargets.Add(key))
                {
                    continue;
                }

                if (tickOnly)
                {
                    PotionEffectApplier.ApplyHealthOnly(potion, target, position);

                    if (trapKeyword != null)
                    {
                        PotionEffectApplier.ApplyHealthOnly(trapKeyword, target, position);
                    }
                }
                else
                {
                    PotionEffectApplier.Apply(potion, target, position);

                    if (trapKeyword != null)
                    {
                        PotionEffectApplier.Apply(trapKeyword, target, position);
                    }
                }

                targetCount++;
            }

#if UNITY_EDITOR
            string trapInfo = trapKeyword != null ? $" + 함정 키워드 {trapKeyword.KeywordName}({trapKeyword.KeywordID})" : "";
            Debug.Log($"[PotionExplosion] : {potion.potionName}({potion.potionID}){trapInfo} 폭발 / 반경 {radius}m / 대상 {targetCount}개");
#endif

            return targetCount;
        }

        /// <summary>
        /// 포션과 함정 키워드의 체력 대미지 효과 수치를 합칩니다. 회복은 제외합니다. (남의 피격 몸 연출 세기용)
        /// </summary>
        private static int GetDamageAmount(PotionData potion, TrapPotionKeyword keyword)
        {
            int total = SumDamage(potion != null ? potion.effects : null);
            total += SumDamage(keyword != null ? keyword.Effects : null);
            return total;
        }

        private static int SumDamage(IReadOnlyList<PotionEffect> effects)
        {
            if (effects == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is HealthChangePotionEffect health
                    && health.healthChange.changeType == HealthChangeType.Damage)
                {
                    total += health.healthChange.amount;
                }
            }

            return total;
        }

        /// <summary>
        /// 남의 플레이어 캐릭터에 피격 몸 연출을 재생합니다. 연출 컴포넌트가 없으면 아무것도 하지 않습니다.
        /// </summary>
        private static void PlayRemoteHitFeedback(Player player, int damage)
        {
            if (player.TryGetComponent(out PlayerHitFeedbackRelay relay))
            {
                relay.PlayBodyFeedbackApprox(damage);
            }
        }

        /// <summary>
        /// 이 컨테이너가 내 플레이어의 컨테이너인지 확인합니다. 다른 플레이어나 몬스터는 false 입니다.
        /// </summary>
        private static bool IsLocalPlayerContainer(BuffContainer container)
        {
            return container != null
                && SurviveStatusManager.Instance != null
                && container == SurviveStatusManager.Instance.PlayerBuffContainer;
        }

        /// <summary>
        /// 같은 대상을 구분하는 키입니다. 대상이 가진 컴포넌트 중 우선순위가 가장 높은 것을 사용합니다.
        /// 한 대상의 여러 콜라이더는 같은 컴포넌트를 찾으므로 같은 키가 됩니다.
        /// 반환값이 null 이면 효과 대상이 아니므로 호출하는 쪽에서 건너뜁니다.
        /// </summary>
        private static Object GetTargetKey(in PotionTarget target)
        {
            if (target.BuffContainer != null)
            {
                return target.BuffContainer;
            }

            if (target.Damageable is Component damageableComponent)
            {
                return damageableComponent;
            }

            return target.WorldObject;
        }
    }
}
