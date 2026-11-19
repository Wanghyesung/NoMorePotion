using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 내 플레이어가 어느 환경 지역(EnvironmentZone)에 있는지 판정해서 SurviveStatusManager 의 온도/밝기 환경을 바꿉니다. 씬에 한 개 둡니다.
    ///
    /// 멀티플레이 : 환경은 각자의 생존 상태에만 영향을 주므로 각 클라이언트가 자기 플레이어만 판정합니다. 네트워크 메시지는 없습니다.
    /// 트리거 이벤트 대신 위치를 주기적으로 검사합니다. 사망, 순간이동, 지역이 꺼질 때 나가는 이벤트를 놓치지 않기 위해서입니다.
    /// 어떤 지역에도 없으면 기본 환경으로 돌아갑니다. 플레이어가 아직 연결되지 않았으면 아무것도 바꾸지 않습니다.
    ///
    /// 판정 간격 기본 0.5초 : 환경 효과가 3~5초 주기로 수치를 바꾸므로 진입 후 최대 0.5초 늦어도 체감 차이가 없습니다.
    ///
    /// 주의 : 켜져 있는 동안은 SurviveStatusManager 의 "테스트: 환경 바꾸기" 버튼 값을 다음 판정에서 덮어씁니다. 버튼으로 시험할 때는 이 컴포넌트를 끄세요.
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        #region Config
        [SerializeField, LabelText("기본 온도 환경"), BoxGroup("기본값")]
        private TemperatureEnvironment _defaultTemperature = TemperatureEnvironment.Normal;

        [SerializeField, LabelText("기본 밝기"), BoxGroup("기본값")]
        private BrightnessLevel _defaultBrightness = BrightnessLevel.Bright;

        [SerializeField, LabelText("판정 간격 (초, 0 = 매 프레임)"), MinValue(0f), BoxGroup("판정")]
        private float _checkInterval = 0.5f;
        #endregion

        #region State
        private float _timer;

        [ShowInInspector, ReadOnly, LabelText("온도를 정한 지역"), BoxGroup("현재")]
        public EnvironmentZone TemperatureZone { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("밝기를 정한 지역"), BoxGroup("현재")]
        public EnvironmentZone BrightnessZone { get; private set; }
        #endregion

        #region Lifecycle
        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            _timer = _checkInterval;
            Evaluate();
        }
        #endregion

        #region Evaluate
        /// <summary>
        /// 내 플레이어 위치로 지금 환경을 판정해 적용합니다. 값이 그대로면 SurviveStatusManager 쪽에서 아무 일도 일어나지 않습니다.
        /// </summary>
        [Button("지금 판정"), BoxGroup("현재")]
        public void Evaluate()
        {
            SurviveStatusManager status = SurviveStatusManager.Instance;
            if (status == null)
            {
                return;
            }

            Transform player = status.BoundPlayerTransform;
            if (player == null)
            {
                return;
            }

            Vector3 position = player.position;
            EnvironmentZone temperatureZone = null;
            EnvironmentZone brightnessZone = null;

            var zones = EnvironmentZone.ActiveZones;
            for (int i = 0; i < zones.Count; i++)
            {
                EnvironmentZone zone = zones[i];
                if (zone == null || !zone.Contains(position))
                {
                    continue;
                }

                if (zone.OverridesTemperature && Wins(zone, temperatureZone))
                {
                    temperatureZone = zone;
                }

                if (zone.OverridesBrightness && Wins(zone, brightnessZone))
                {
                    brightnessZone = zone;
                }
            }

            TemperatureZone = temperatureZone;
            BrightnessZone = brightnessZone;

            status.SetTemperatureEnvironment(temperatureZone != null ? temperatureZone.Temperature : _defaultTemperature);
            status.SetBrightness(brightnessZone != null ? brightnessZone.Brightness : _defaultBrightness);
        }

        // 우선순위가 크면 이기고, 같으면 부피가 작은(안쪽) 지역이 이깁니다.
        private static bool Wins(EnvironmentZone candidate, EnvironmentZone current)
        {
            if (current == null)
            {
                return true;
            }

            if (candidate.Priority != current.Priority)
            {
                return candidate.Priority > current.Priority;
            }

            return candidate.WorldVolume < current.WorldVolume;
        }
        #endregion
    }
}
