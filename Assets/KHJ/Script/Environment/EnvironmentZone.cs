using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 맵에 고정된 환경 지역(더운 지역, 추운 지역, 동굴 등)입니다. 박스 범위 안에 들어가면 바로 그 환경이 적용됩니다.
    /// 판정과 적용은 EnvironmentManager 가 합니다. 이 컴포넌트는 범위와 값만 가집니다.
    ///
    /// 기획 규칙
    /// - 환경은 맵에 고정되어 있고, 경계에 들어서면 바로 바뀝니다.
    /// - 환경은 온도(Normal / Cold / Hot)와 밝기(Bright / Dim / Dark) 두 가지입니다. 두 축은 서로 따로 정합니다.
    ///   (예: 추운 지역 안의 동굴은 밝기만 Dark 로 지정하고, 온도는 바깥 추운 지역을 따름)
    ///
    /// 겹침 : 축마다 "그 축을 지정한 지역" 중 우선순위가 큰 것, 같으면 부피가 작은 것(안쪽 지역)이 이깁니다.
    /// 범위는 콜라이더가 아니라 이 컴포넌트의 박스(회전, 크기 반영)입니다. 큰 트리거가 레이캐스트나 포션 탐지에 걸리지 않게 하려는 것입니다.
    /// 모양이 복잡한 지역은 같은 설정의 지역을 여러 개 놓아 박스로 덮으세요.
    /// </summary>
    public class EnvironmentZone : MonoBehaviour
    {
        #region Config
        [SerializeField, LabelText("온도 지정"), BoxGroup("환경")]
        private bool _overrideTemperature;

        [SerializeField, LabelText("온도 환경"), ShowIf(nameof(_overrideTemperature)), BoxGroup("환경")]
        private TemperatureEnvironment _temperature = TemperatureEnvironment.Normal;

        [SerializeField, LabelText("밝기 지정"), BoxGroup("환경")]
        private bool _overrideBrightness;

        [SerializeField, LabelText("밝기 환경"), ShowIf(nameof(_overrideBrightness)), BoxGroup("환경")]
        private BrightnessLevel _brightness = BrightnessLevel.Bright;

        [SerializeField, LabelText("우선순위"), BoxGroup("겹침 (클수록 우선, 같으면 작은 지역 우선)")]
        private int _priority;

        [SerializeField, LabelText("박스 중심 (로컬)"), BoxGroup("범위")]
        private Vector3 _center;

        [SerializeField, LabelText("박스 크기 (로컬)"), BoxGroup("범위")]
        private Vector3 _size = new Vector3(20f, 10f, 20f);

        public bool OverridesTemperature => _overrideTemperature;
        public TemperatureEnvironment Temperature => _temperature;
        public bool OverridesBrightness => _overrideBrightness;
        public BrightnessLevel Brightness => _brightness;
        public int Priority => _priority;
        #endregion

        #region Registry
        // 켜져 있는 지역 목록입니다. 매니저가 매번 FindObjects 를 하지 않도록 지역이 직접 등록합니다.
        private static readonly List<EnvironmentZone> _activeZones = new List<EnvironmentZone>();

        /// <summary>
        /// 지금 켜져 있는 환경 지역 목록입니다.
        /// </summary>
        public static IReadOnlyList<EnvironmentZone> ActiveZones => _activeZones;

        // 도메인 리로드를 끈 Play 모드에서 이전 실행의 목록이 남지 않도록 비웁니다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistry()
        {
            _activeZones.Clear();
        }

        private void OnEnable()
        {
            if (!_activeZones.Contains(this))
            {
                _activeZones.Add(this);
            }
        }

        private void OnDisable()
        {
            _activeZones.Remove(this);
        }
        #endregion

        #region Query
        /// <summary>
        /// 월드 좌표가 이 지역의 박스 안에 있는지 판정합니다. (경계 위는 안으로 봄)
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition) - _center;
            Vector3 half = _size * 0.5f;

            return Mathf.Abs(local.x) <= Mathf.Abs(half.x)
                && Mathf.Abs(local.y) <= Mathf.Abs(half.y)
                && Mathf.Abs(local.z) <= Mathf.Abs(half.z);
        }

        /// <summary>
        /// 월드 기준 박스 부피입니다. 우선순위가 같을 때 안쪽(작은) 지역을 고르는 데 씁니다.
        /// </summary>
        public float WorldVolume
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                return Mathf.Abs(_size.x * scale.x * _size.y * scale.y * _size.z * scale.z);
            }
        }
        #endregion

        #region Gizmos
        private void OnDrawGizmos()
        {
            DrawBox(0.05f);
        }

        private void OnDrawGizmosSelected()
        {
            DrawBox(0.2f);
        }

        // 온도로 색을 정합니다. (더움 = 빨강, 추움 = 파랑, 온도 지정 없음 = 밝기만 지정한 지역이라 보라)
        private void DrawBox(float fillAlpha)
        {
            Color color = !_overrideTemperature ? new Color(0.6f, 0.3f, 0.9f)
                : _temperature == TemperatureEnvironment.Hot ? new Color(1f, 0.35f, 0.2f)
                : _temperature == TemperatureEnvironment.Cold ? new Color(0.3f, 0.6f, 1f)
                : Color.white;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(color.r, color.g, color.b, fillAlpha);
            Gizmos.DrawCube(_center, _size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(_center, _size);
        }
        #endregion
    }
}
