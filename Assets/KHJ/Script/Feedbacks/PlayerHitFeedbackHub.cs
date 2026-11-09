using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 내 플레이어가 피격당했을 때의 화면 연출(붉은 비네팅, 플래시, 카메라 흔들림 등)을 재생하는 허브입니다. 씬에 한 개만 배치합니다.
    /// 연출 내용은 연결한 MMF_Player 에 구성하고, 이 클래스는 "얼마나 세게 재생할지"만 정합니다.
    ///
    /// 세기는 "받은 피해 / 플레이어 최대 체력" 비율로 정합니다. 약한 피격은 최소 세기, 강한 피격은 최대 세기로 재생하고,
    /// 아주 작은 피해(비율이 너무 낮은 지속 피해 등)는 재생하지 않습니다.
    /// 짧은 시간에 피격이 겹치면 더 센 것만 재생해서 후처리가 과하게 겹치지 않게 합니다.
    ///
    /// 허브가 씬에 없으면 아무 일도 일어나지 않습니다. (null 안전)
    /// 피격을 이 허브에 알려 주는 것은 PlayerHitFeedbackRelay 입니다.
    /// </summary>
    public class PlayerHitFeedbackHub : MonoBehaviour
    {
        public static PlayerHitFeedbackHub Instance { get; private set; }

        [SerializeField, LabelText("피격 연출 플레이어"), Required("재생할 MMF_Player 를 연결해 주세요.")]
        private MMF_Player _player;

        [SerializeField, LabelText("약한 피격 세기"), BoxGroup("세기"), MinValue(0f)]
        private float _lightIntensity = 0.4f;

        [SerializeField, LabelText("강한 피격 세기"), BoxGroup("세기"), MinValue(0f)]
        private float _heavyIntensity = 1.5f;

        // 이 비율(받은 피해 / 최대 체력) 이하는 약한 피격 세기, 이상은 강한 피격 세기이고 그 사이는 보간합니다.
        [SerializeField, LabelText("약한 피격 기준 (최대 체력 대비)"), BoxGroup("세기"), Range(0f, 1f)]
        private float _lightRatio = 0.02f;

        [SerializeField, LabelText("강한 피격 기준 (최대 체력 대비)"), BoxGroup("세기"), Range(0f, 1f)]
        private float _heavyRatio = 0.3f;

        [SerializeField, LabelText("이 비율 미만이면 재생 안 함"), BoxGroup("겹침 제어"), Range(0f, 1f)]
        private float _ignoreBelowRatio = 0.005f;

        [SerializeField, LabelText("겹침 판정 시간 (초)"), BoxGroup("겹침 제어"), MinValue(0f)]
        private float _overlapWindowSeconds = 0.15f;

        // 마지막으로 재생한 시각과 세기입니다. 겹침 판정에 씁니다.
        private float _lastPlayTime = float.NegativeInfinity;
        private float _lastIntensity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerHitFeedbackHub] : 씬에 허브가 두 개 이상 있어 새로 생긴 것을 제거합니다.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 허브가 씬에 있으면 피격 연출을 재생합니다. 없으면 아무것도 하지 않습니다.
        /// </summary>
        /// <param name="damageRatio">받은 피해 / 플레이어 최대 체력</param>
        public static void Play(float damageRatio)
        {
            if (Instance != null)
            {
                Instance.PlayInternal(damageRatio);
            }
        }

        private void PlayInternal(float damageRatio)
        {
            if (_player == null || damageRatio < _ignoreBelowRatio)
            {
                return;
            }

            float intensity = GetIntensity(damageRatio);

            // 짧은 시간 안에 더 약한 피격이 또 오면 무시합니다. (후처리 연출이 겹쳐서 과해지는 것을 막음)
            float now = Time.unscaledTime;
            if (now - _lastPlayTime < _overlapWindowSeconds && intensity <= _lastIntensity)
            {
                return;
            }

            _lastPlayTime = now;
            _lastIntensity = intensity;

            _player.PlayFeedbacks(transform.position, intensity);
        }

        /// <summary>
        /// 피해 비율을 약한 피격 ~ 강한 피격 사이의 세기로 바꿉니다.
        /// </summary>
        private float GetIntensity(float damageRatio)
        {
            float t = Mathf.InverseLerp(_lightRatio, _heavyRatio, damageRatio);
            return Mathf.Lerp(_lightIntensity, _heavyIntensity, t);
        }

#if UNITY_EDITOR
        // ==================== 에디터 테스트용 ============================

        [Button("테스트: 피격 재생", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void PlayForTest(float damageRatio = 0.1f)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PlayerHitFeedbackHub] : Play 모드에서만 테스트할 수 있습니다.");
                return;
            }

            PlayInternal(damageRatio);
        }
#endif
    }
}
