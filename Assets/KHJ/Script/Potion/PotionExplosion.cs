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
        #region Buffers
        // 한 번의 폭발이 동시에 찾을 수 있는 콜라이더 수입니다. 가득 차면 경고합니다.
        private const int MaxColliders = 64;

        // 매 폭발마다 새로 할당하지 않도록 재사용하는 버퍼입니다.
        private static readonly Collider[] ColliderBuffer = new Collider[MaxColliders];
        private static readonly HashSet<Object> AppliedTargets = new HashSet<Object>();
        #endregion

        #region Explode
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
        /// <param name="damageMultiplier">체력 대미지 효과에만 곱하는 배율입니다. (던진 사람의 공격력 배율) 1 이면 그대로입니다.</param>
        public static int Explode(PotionInstance instance, Vector3 position, PotionTargetScope scope = PotionTargetScope.All, float damageMultiplier = 1f)
        {
            if (instance == null)
            {
                Debug.LogWarning("[PotionExplosion]: 폭발시킬 포션 인스턴스가 null 입니다.");
                return 0;
            }

            return ExplodeCore(instance.Data, instance.TrapKeyword, position, false, scope, damageMultiplier);
        }

        /// <summary>
        /// 범위 장판이 간격마다 반복하는 틱입니다. 지금 범위 안에 있는 대상에게만 체력 증감(초당 대미지 등) 효과를 적용합니다.
        /// 처음 폭발(Explode)에서 이미 적용한 상태 효과, 생존 상태 증감, 월드 오브젝트 대미지는 반복하지 않고, 화면 연출도 다시 재생하지 않습니다.
        /// </summary>
        public static int ExplodeTick(PotionInstance instance, Vector3 position, PotionTargetScope scope = PotionTargetScope.All, float damageMultiplier = 1f)
        {
            if (instance == null)
            {
                return 0;
            }

            return ExplodeCore(instance.Data, instance.TrapKeyword, position, true, scope, damageMultiplier);
        }

        /// <summary>
        /// 내 플레이어에게만 적용하는 폭발입니다. (scope 가 LocalPlayer 인 Explode/ExplodeTick 과 같음)
        /// </summary>
        /// <param name="potion">효과와 폭발 범위의 기준이 되는 원래 포션</param>
        /// <param name="keyword">함정 키워드. 있으면 그 효과도 같이 적용합니다. (범위는 원래 포션을 따름)</param>
        /// <param name="tickOnly">true 면 체력 증감 효과만 적용하고 연출은 재생하지 않습니다. (범위 장판의 반복 틱)</param>
        public static int ExplodeLocalPlayer(PotionData potion, TrapPotionKeyword keyword, Vector3 position, bool tickOnly, float damageMultiplier = 1f)
        {
            return ExplodeCore(potion, keyword, position, tickOnly, PotionTargetScope.LocalPlayer, damageMultiplier);
        }
        #endregion

        #region Area
        /// <summary>
        /// position 에서 radius 안의 대상에게 체력 증감 효과 하나와 상태 이상 목록을 적용하고, 닿은 대상 수를 반환합니다.
        /// 재해 장판(블랙홀 등)이 단계마다(처음, 틱, 마지막) 대미지를 줄 때 부릅니다.
        /// 대상 구분(scope)은 폭발과 같고, 폭발 화면 연출은 재생하지 않습니다.
        /// </summary>
        /// <param name="health">적용할 체력 증감 효과. null 이면 대미지는 주지 않습니다.</param>
        /// <param name="statuses">함께 걸 상태 이상 목록. null 이거나 비어 있으면 걸지 않습니다. 같은 효과를 틱마다 다시 걸어도 중첩 정책이 처리합니다.</param>
        /// <param name="damageMultiplier">체력 대미지에 곱하는 배율입니다. (던진 사람의 공격력 배율) 상태 이상에는 곱하지 않습니다.</param>
        public static int ApplyZonePhase(HealthChangePotionEffect health, IReadOnlyList<ApplyStatusPotionEffect> statuses, Vector3 position, float radius, PotionTargetScope scope = PotionTargetScope.All, float damageMultiplier = 1f)
        {
            bool hasStatuses = statuses != null && statuses.Count > 0;
            if (health == null && !hasStatuses)
            {
                return 0;
            }

            PotionExplosionConfig config = PotionExplosionConfig.Instance;
            if (config == null)
            {
                Debug.LogWarning($"[PotionExplosion]: 폭발 설정 에셋이 없습니다. (Resources/{PotionExplosionConfig.ResourcePath})");
                return 0;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(position, radius, ColliderBuffer, config.TargetLayers, QueryTriggerInteraction.Collide);

            if (hitCount >= MaxColliders)
            {
                Debug.LogWarning($"[PotionExplosion]: 범위 안의 콜라이더가 {MaxColliders}개 이상이라 일부가 누락될 수 있습니다.");
            }

            AppliedTargets.Clear();
            int targetCount = 0;

            // 남의 플레이어 몸 연출에 쓸 피해 수치입니다. (대미지일 때만)
            int visualDamage = health != null && health.healthChange.changeType == HealthChangeType.Damage
                ? Mathf.RoundToInt(health.healthChange.amount * damageMultiplier)
                : 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = ColliderBuffer[i];
                ColliderBuffer[i] = null;   // 파괴된 오브젝트를 붙잡지 않도록 비웁니다.

                if (!TryPrepareTarget(collider, scope, visualDamage, out PotionTarget target))
                {
                    continue;
                }

                // 대미지를 먼저 주고 상태 이상을 겁니다. 상태 이상의 주체(source)는 효과 객체 자신이라, 틱마다 다시 걸어도 같은 주체로 처리됩니다.
                PotionEffectApplier.ApplyHealth(health, health, target, position, damageMultiplier);

                if (hasStatuses)
                {
                    for (int s = 0; s < statuses.Count; s++)
                    {
                        PotionEffectApplier.ApplyStatus(statuses[s], statuses[s], target);
                    }
                }

                targetCount++;
            }

            return targetCount;
        }
        #endregion

        #region Core
        private static int ExplodeCore(PotionData potion, TrapPotionKeyword trapKeyword, Vector3 position, bool tickOnly = false, PotionTargetScope scope = PotionTargetScope.All, float damageMultiplier = 1f)
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

            // 남의 플레이어 몸 연출에 쓸 피해 수치입니다. (효과 적용과 무관한 연출 전용, 공격력 배율 반영)
            int visualDamage = Mathf.RoundToInt(GetDamageAmount(potion, trapKeyword) * damageMultiplier);

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = ColliderBuffer[i];
                ColliderBuffer[i] = null;   // 파괴된 오브젝트를 붙잡지 않도록 비웁니다.

                // 대상 구성, 범위 구분, 중복 제거는 공용 도우미가 합니다. false 면 이 대상은 건너뜁니다.
                if (!TryPrepareTarget(collider, scope, visualDamage, out PotionTarget target))
                {
                    continue;
                }

                if (tickOnly)
                {
                    PotionEffectApplier.ApplyHealthOnly(potion, target, position, damageMultiplier);

                    if (trapKeyword != null)
                    {
                        PotionEffectApplier.ApplyHealthOnly(trapKeyword, target, position, damageMultiplier);
                    }
                }
                else
                {
                    PotionEffectApplier.Apply(potion, target, position, damageMultiplier);

                    if (trapKeyword != null)
                    {
                        PotionEffectApplier.Apply(trapKeyword, target, position, damageMultiplier);
                    }
                }

                targetCount++;
            }

            // 블랙홀 같은 재해 소환 효과는 폭발당 한 번, 처음 폭발에서만 소환합니다. (반복 틱에서는 소환하지 않음)
            // 대상 루프가 끝난 뒤에 소환해야 합니다. 소환된 장판이 같은 정적 버퍼(ColliderBuffer, AppliedTargets)를 쓰기 때문입니다.
            if (!tickOnly)
            {
                SpawnZones(potion.effects, position, radius, scope, damageMultiplier);
                SpawnZones(trapKeyword != null ? trapKeyword.Effects : null, position, radius, scope, damageMultiplier);
            }

#if UNITY_EDITOR
            string trapInfo = trapKeyword != null ? $" + 함정 키워드 {trapKeyword.KeywordName}({trapKeyword.KeywordID})" : "";
            Debug.Log($"[PotionExplosion] : {potion.potionName}({potion.potionID}){trapInfo} 폭발 / 반경 {radius}m / 대상 {targetCount}개");
#endif

            return targetCount;
        }
        #endregion

        #region RemoteVisual
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
        #endregion

        #region Helpers
        /// <summary>
        /// 콜라이더 하나에서 효과 대상을 만들고, 이 기기가 처리할 대상인지 판정합니다. 처리할 대상이면 true 입니다.
        /// 범위 구분 : 플레이어는 내 플레이어만(남의 플레이어는 그 기기가 처리), 플레이어가 아닌 대상은 NonPlayers 일 때만 처리합니다.
        /// 한 대상의 여러 콜라이더는 같은 키가 되어 한 번만 처리합니다. (AppliedTargets 는 호출하는 쪽이 미리 비웁니다)
        /// </summary>
        /// <param name="visualDamage">남의 플레이어 몸 연출에 쓸 피해 수치. 0 이하면 연출하지 않습니다.</param>
        private static bool TryPrepareTarget(Collider collider, PotionTargetScope scope, int visualDamage, out PotionTarget target)
        {
            // 컨테이너는 Owner 로 찾습니다. (플레이어 컨테이너는 콜라이더의 부모 계층에 없을 수 있음)
            BuffContainer container = BuffContainer.FindByOwner(collider.transform);

            target = new PotionTarget
            {
                BuffContainer = container,
                Damageable = collider.GetComponentInParent<IDamageable>(),
                WorldObject = collider.GetComponentInParent<WorldObject>(),
                IsLocalPlayer = IsLocalPlayerContainer(container),
            };

            if (target.Damageable is Player player)
            {
                if (!player.IsLocalPlayer)
                {
                    // 남의 플레이어에게는 효과를 적용하지 않습니다. 대신 이 기기에서 보이는 대로 몸 연출만 근사해서 재생합니다.
                    if (visualDamage > 0 && AppliedTargets.Add(player))
                    {
                        PlayRemoteHitFeedback(player, visualDamage);
                    }

                    return false;
                }

                if ((scope & PotionTargetScope.LocalPlayer) == 0)
                {
                    return false;
                }
            }
            else if ((scope & PotionTargetScope.NonPlayers) == 0)
            {
                return false;
            }

            Object key = GetTargetKey(target);
            return key != null && AppliedTargets.Add(key);
        }

        /// <summary>
        /// 효과 목록에서 재해 소환 효과를 찾아 소환합니다. 목록이 null 이면 아무것도 하지 않습니다.
        /// </summary>
        private static void SpawnZones(IReadOnlyList<PotionEffect> effects, Vector3 position, float radius, PotionTargetScope scope, float damageMultiplier)
        {
            if (effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is SummonDisasterPotionEffect summon)
                {
                    SpawnDisaster(summon, position, radius, scope, damageMultiplier);
                }
            }
        }

        private static void SpawnDisaster(SummonDisasterPotionEffect summon, Vector3 position, float radius, PotionTargetScope scope, float damageMultiplier)
        {
            if (summon.disasterPrefab == null)
            {
                Debug.LogWarning("[PotionExplosion]: 재해 프리팹이 비어 있어 소환하지 않습니다.");
                return;
            }

            if (PoolManager.Instance == null)
            {
                Debug.LogWarning("[PotionExplosion]: PoolManager 가 없어 재해를 소환하지 못했습니다.");
                return;
            }

            GameObject zoneObject = PoolManager.Instance.Get(summon.disasterPrefab, position, Quaternion.identity);
            if (zoneObject == null)
            {
                return;
            }

            if (!zoneObject.TryGetComponent(out DisasterZone zone))
            {
                Debug.LogWarning($"[PotionExplosion]: 재해 프리팹({summon.disasterPrefab.name})에 DisasterZone 컴포넌트가 없어 반납합니다.");
                PoolManager.Instance.Release(zoneObject);
                return;
            }

            zone.Initialize(summon, radius, scope, damageMultiplier);
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
        #endregion
    }
}
