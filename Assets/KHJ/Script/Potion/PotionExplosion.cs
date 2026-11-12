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
        public static int Explode(PotionInstance instance, Vector3 position)
        {
            if (instance == null)
            {
                Debug.LogWarning("[PotionExplosion]: 폭발시킬 포션 인스턴스가 null 입니다.");
                return 0;
            }

            return ExplodeCore(instance.Data, instance.TrapKeyword, position);
        }

        /// <summary>
        /// 범위 장판이 간격마다 반복하는 틱입니다. 지금 범위 안에 있는 대상에게만 체력 증감(초당 대미지 등) 효과를 적용합니다.
        /// 처음 폭발(Explode)에서 이미 적용한 상태 효과, 생존 상태 증감, 월드 오브젝트 대미지는 반복하지 않고, 화면 연출도 다시 재생하지 않습니다.
        /// </summary>
        public static int ExplodeTick(PotionInstance instance, Vector3 position)
        {
            if (instance == null)
            {
                return 0;
            }

            return ExplodeCore(instance.Data, instance.TrapKeyword, position, true);
        }

        /// <param name="potion">효과와 폭발 범위의 기준이 되는 원래 포션</param>
        /// <param name="trapKeyword">함정 키워드. 있으면 그 효과도 같이 적용합니다. (범위는 원래 포션을 따름)</param>
        /// <param name="tickOnly">true 면 체력 증감 효과만 적용하고 연출은 재생하지 않습니다. (범위 장판의 반복 틱)</param>
        public static int ExplodeLocalPlayer(PotionData potion, TrapPotionKeyword keyword, Vector3 position, bool tickOnly)
        {
            return ExplodeCore(potion, keyword, position, tickOnly, true);
        }

        private static int ExplodeCore(PotionData potion, TrapPotionKeyword trapKeyword, Vector3 position, bool tickOnly = false, bool localPlayerOnly = false)
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

                if (target.Damageable is Player player)
                {
                    if (!player.IsLocalPlayer)
                        continue;
                }
                else if (localPlayerOnly)
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
