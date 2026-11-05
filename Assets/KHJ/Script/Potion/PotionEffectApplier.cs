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

        // 생존 상태(수분 등)를 적용할 대상인지 여부입니다. 현재는 Player 레이어이면 true 입니다.
        public bool IsPlayer;
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

            IReadOnlyList<PotionEffect> effects = potion.effects;

            for (int i = 0; i < effects.Count; i++)
            {
                switch (effects[i])
                {
                    case ApplyStatusPotionEffect status:
                        if (target.BuffContainer != null)
                        {
                            // 건 주체를 포션 SO 로 넘겨서, 같은 포션을 다시 맞아도 중첩 정책이 의도대로 동작하게 합니다.
                            target.BuffContainer.Apply(status.statusEffect, potion, status.percent, status.durationSeconds);
                        }
                        break;

                    case HealthChangePotionEffect health:
                        // 지속시간이 있는 체력 증감(초당 대미지)은 필드에 만드는 별도 오브젝트가 맡으므로 여기서는 즉시 효과만 적용합니다.
                        if (target.Damageable != null && health.durationSeconds <= 0f)
                        {
                            health.healthChange.Apply(target.Damageable, explosionPosition);
                        }
                        break;

                    case GaugeChangePotionEffect gauge:
                        // 생존 상태는 0~100 범위라서 퍼센트가 곧 증감 수치입니다.
                        if (target.IsPlayer && SurviveStatusManager.Instance != null)
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
