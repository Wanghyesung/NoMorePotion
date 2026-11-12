using System.Collections.Generic;
using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 폭발에 닿은 대상 한 개가 가진 컴포넌트 묶음입니다. 없는 컴포넌트는 null 입니다.
    /// </summary>
    public struct PotionTarget
    {
        public BuffContainer BuffContainer;
        public IDamageable Damageable;
        public WorldObject WorldObject;

        // 내 플레이어인지 여부입니다. 생존 상태(수분 등)는 내 플레이어에게만 적용합니다. (다른 플레이어, 몬스터는 false)
        public bool IsLocalPlayer;
    }

    /// <summary>
    /// 포션의 효과 목록을 대상 한 개에게 적용하는 정적 도우미입니다.
    /// 효과 종류마다 대상이 해당 컴포넌트를 가지고 있을 때만 적용하고, 없으면 건너뜁니다.
    /// 범위 탐색은 PotionExplosion 이 맡고, 이 클래스는 "이 대상에게 무엇을 적용하는가"만 맡습니다.
    /// </summary>
    public static class PotionEffectApplier
    {
        /// <param name="potion">적용할 포션 데이터</param>
        /// <param name="target">효과를 받는 대상</param>
        /// <param name="explosionPosition">폭발 위치. 대미지의 공격 위치로 전달합니다.</param>
        public static void Apply(PotionData potion, in PotionTarget target, Vector3 explosionPosition)
        {
            if (potion == null)
            {
                Debug.LogWarning("[PotionEffectApplier]: 적용할 포션 데이터가 null 입니다.");
                return;
            }

            Apply(potion.effects, potion, target, explosionPosition);
        }

        /// <summary>
        /// 포션의 체력 증감(대미지, 회복) 효과만 대상 한 개에게 적용합니다. 상태 효과, 생존 상태 증감, 월드 오브젝트 대미지는 건너뜁니다.
        /// 범위 장판이 간격마다 반복해서 적용하는 틱에 쓰며, 처음 폭발 때 이미 적용한 나머지 효과가 반복되지 않게 합니다.
        /// </summary>
        public static void ApplyHealthOnly(PotionData potion, in PotionTarget target, Vector3 explosionPosition)
        {
            if (potion == null)
            {
                return;
            }

            Apply(potion.effects, potion, target, explosionPosition, true);
        }

        /// <summary>
        /// 함정 키워드의 체력 증감 효과만 대상 한 개에게 적용합니다. (범위 장판의 반복 틱용)
        /// </summary>
        public static void ApplyHealthOnly(TrapPotionKeyword keyword, in PotionTarget target, Vector3 explosionPosition)
        {
            if (keyword == null)
            {
                return;
            }

            Apply(keyword.Effects, keyword, target, explosionPosition, true);
        }

        /// <summary>
        /// 함정 키워드의 효과를 대상 한 개에게 적용합니다. (원래 포션의 효과에 더해서 적용)
        /// </summary>
        public static void Apply(TrapPotionKeyword keyword, in PotionTarget target, Vector3 explosionPosition)
        {
            if (keyword == null)
            {
                Debug.LogWarning("[PotionEffectApplier]: 적용할 함정 키워드가 null 입니다.");
                return;
            }

            Apply(keyword.Effects, keyword, target, explosionPosition);
        }

        /// <param name="effects">적용할 효과 목록</param>
        /// <param name="source">효과를 건 주체(포션 또는 키워드 SO). 같은 주체가 다시 걸었을 때 중첩 정책이 의도대로 동작하게 합니다.</param>
        /// <param name="healthOnly">true 면 체력 증감 효과만 적용합니다. (범위 장판의 반복 틱)</param>
        private static void Apply(IReadOnlyList<PotionEffect> effects, Object source, in PotionTarget target, Vector3 explosionPosition, bool healthOnly = false)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (healthOnly && !(effects[i] is HealthChangePotionEffect))
                {
                    continue;
                }

                switch (effects[i])
                {
                    case ApplyStatusPotionEffect status:
                        if (target.BuffContainer != null)
                        {
                            target.BuffContainer.Apply(status.statusEffect, source, status.percent, status.durationSeconds);
                        }
                        break;

                    case HealthChangePotionEffect health:
                        // 한 번(한 틱)만 적용한다. 초당 대미지처럼 반복되는 효과는 범위 장판이 간격마다 다시 적용하므로, 범위를 벗어나면 멈춘다.
                        if (target.BuffContainer != null)
                        {
                            target.BuffContainer.ApplyPotionHealth(health, source, explosionPosition);
                        }
                        else if (target.Damageable != null)
                        {
                            health.healthChange.Apply(target.Damageable, explosionPosition);
                        }
                        break;

                    case GaugeChangePotionEffect gauge:
                        // 생존 상태는 0~100 범위라서 퍼센트가 곧 증감 수치입니다.
                        if (target.IsLocalPlayer && SurviveStatusManager.Instance != null)
                        {
                            SurviveStatusManager.Instance.ChangeStatus(gauge.gauge, gauge.percent);
                        }
                        break;

                    case WorldObjectDamagePotionEffect worldObjectDamage:
                        if (target.WorldObject != null)
                        {
                            target.WorldObject.ReceivePotionDamage(worldObjectDamage);
                        }
                        break;
                }
            }
        }
    }
}
