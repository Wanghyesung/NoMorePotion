using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 폭발 위치에 재해(블랙홀, 블리자드, 메테오 등 범위 지속 공격)를 소환하는 효과입니다. 프리팹과 수치만 바꿔서 여러 재해를 만듭니다.
    ///
    /// 기획 : 소환되는 순간 강한 대미지 1회 → 지속시간 동안 일정 간격(기본 0.5초)마다 중간 대미지 → 끝나는 순간 큰 대미지 1회.
    /// - 상태 이상(예: 블랙홀의 중력 = 이동속도 감소)은 소환 순간과 틱마다 범위 안의 대상에게 걸립니다.
    /// - 범위는 원래 포션의 폭발 범위(소/중/대)를 따릅니다.
    /// - 대상별로 도는 PotionEffectApplier 가 아니라 PotionExplosion 이 폭발당 한 번만 소환합니다.
    /// - 세 대미지는 모두 체력 대미지라서 던진 사람의 공격력 배율이 곱해집니다. (상태 이상 수치에는 곱하지 않음)
    ///
    /// 설정 주의
    /// - 세 대미지의 "체력 증감 종류"는 Damage 로 정해야 합니다. (None 이면 아무 일도 일어나지 않음)
    /// - 틱 대미지의 "반복 간격"은 0보다 크게 두세요. 주기 대미지로 취급되어 "피격당할 때마다 정신력 감소"가 틱마다 걸리지 않습니다.
    ///   (실제 틱 간격은 아래 "틱 간격"이 정합니다. 모듈의 반복 간격은 주기 대미지 표시용입니다.)
    /// - 상태 이상 SO 의 중첩 정책은 RefreshDuration(지속시간 갱신)으로 두세요. 틱마다 다시 걸리므로 Stackable 이면 틱마다 쌓입니다.
    ///   지속시간은 틱 간격의 2배 이상(예: 1초)이 좋습니다. 범위를 벗어나면 그 시간만큼 더 걸려 있다가 풀립니다.
    /// </summary>
    [Serializable]
    public class SummonDisasterPotionEffect : PotionEffect
    {
        #region Visual
        [SerializeField, LabelText("재해 프리팹"), Required, BoxGroup("연출")]
        [Tooltip("DisasterZone 컴포넌트가 붙은 프리팹입니다. 이펙트는 이 프리팹 안에 둡니다.")]
        private GameObject _disasterPrefab;
        public GameObject disasterPrefab => _disasterPrefab;
        #endregion

        #region Timing
        [SerializeField, LabelText("지속시간 (초)"), MinValue(0.1f), BoxGroup("시간")]
        private float _durationSeconds = 3.5f;
        public float durationSeconds => _durationSeconds;

        [SerializeField, LabelText("틱 간격 (초)"), MinValue(0.05f), BoxGroup("시간")]
        private float _tickIntervalSeconds = 0.5f;
        public float tickIntervalSeconds => _tickIntervalSeconds;
        #endregion

        #region Damage
        [SerializeField, LabelText("처음 대미지 (소환 순간 1회)"), BoxGroup("대미지")]
        private HealthChangePotionEffect _firstHit = new HealthChangePotionEffect();
        public HealthChangePotionEffect firstHit => _firstHit;

        [SerializeField, LabelText("틱 대미지 (간격마다)"), BoxGroup("대미지")]
        private HealthChangePotionEffect _tickHit = new HealthChangePotionEffect();
        public HealthChangePotionEffect tickHit => _tickHit;

        [SerializeField, LabelText("마지막 대미지 (끝날 때 1회)"), BoxGroup("대미지")]
        private HealthChangePotionEffect _finalHit = new HealthChangePotionEffect();
        public HealthChangePotionEffect finalHit => _finalHit;
        #endregion

        #region Status
        // 소환 순간과 틱마다 범위 안의 대상에게 거는 상태 이상입니다. 마지막 대미지 순간에는 걸지 않습니다.
        // 예) 블랙홀 : 중력 디버프(이동속도 감소) 수치 -30 / 지속시간 1초
        [SerializeField, LabelText("상태 이상 (소환 순간과 틱마다 갱신)"), BoxGroup("상태 이상")]
        private List<ApplyStatusPotionEffect> _statusEffects = new List<ApplyStatusPotionEffect>();
        public IReadOnlyList<ApplyStatusPotionEffect> statusEffects => _statusEffects;
        #endregion
    }
}
