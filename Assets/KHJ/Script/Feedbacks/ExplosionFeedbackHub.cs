using Enzeun.SO;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 포션 폭발의 "화면 단위" 연출(카메라 흔들림, 후처리, 화면 플래시, 히트스톱 등)을 재생하는 허브입니다. 씬에 한 개만 배치합니다.
    /// 연출 내용은 연결한 MMF_Player 에 구성하고, 이 클래스는 "얼마나 세게 재생할지"만 정합니다.
    ///
    /// 세기 = 포션 크기별 세기 × 거리 감쇠(내 카메라와 폭발 위치의 거리). 너무 멀거나 약하면 재생하지 않습니다.
    /// 짧은 시간에 폭발이 겹치면 더 센 것만 재생해서 후처리가 과하게 겹치지 않게 합니다.
    /// 위치별 연출(파티클, 폭발음 등)은 포션 데이터의 폭발 프리팹이 맡고, 허브는 로컬에서만 재생하므로 네트워크와 무관합니다.
    ///
    /// 허브가 씬에 없으면 아무 일도 일어나지 않습니다. (null 안전)
    /// </summary>
    public class ExplosionFeedbackHub : MonoBehaviour
    {
        public static ExplosionFeedbackHub Instance { get; private set; }

        [SerializeField, LabelText("화면 연출 플레이어"), Required("재생할 MMF_Player 를 연결해 주세요.")]
        private MMF_Player _player;

        [SerializeField, LabelText("소 세기"), BoxGroup("크기별 세기"), MinValue(0f)]
        private float _smallIntensity = 0.5f;

        [SerializeField, LabelText("중 세기"), BoxGroup("크기별 세기"), MinValue(0f)]
        private float _mediumIntensity = 1f;

        [SerializeField, LabelText("대 세기"), BoxGroup("크기별 세기"), MinValue(0f)]
        private float _largeIntensity = 1.5f;

        [SerializeField, LabelText("연출이 닿는 최대 거리 (m)"), BoxGroup("거리 감쇠"), MinValue(0.1f)]
        private float _maxDistance = 30f;

        // 가로축 : 거리 / 최대 거리 (0~1), 세로축 : 세기 배율
        [SerializeField, LabelText("거리 감쇠 곡선"), BoxGroup("거리 감쇠")]
        private AnimationCurve _distanceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [SerializeField, LabelText("이 세기 미만이면 재생 안 함"), BoxGroup("겹침 제어"), MinValue(0f)]
        private float _minIntensity = 0.05f;

        [SerializeField, LabelText("겹침 판정 시간 (초)"), BoxGroup("겹침 제어"), MinValue(0f)]
        private float _overlapWindowSeconds = 0.1f;

        // 마지막으로 재생한 시각과 세기입니다. 겹침 판정에 씁니다.
        private float _lastPlayTime = float.NegativeInfinity;
        private float _lastIntensity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[ExplosionFeedbackHub] : 씬에 허브가 두 개 이상 있어 새로 생긴 것을 제거합니다.");
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
        /// 허브가 씬에 있으면 폭발 연출을 재생합니다. 없으면 아무것도 하지 않습니다.
        /// </summary>
        public static void Play(Vector3 position, PotionSize size)
        {
            if (Instance != null)
            {
                Instance.PlayInternal(position, size);
            }
        }

        /// <summary>
        /// 포션 크기와 내 카메라까지의 거리로 세기를 정해 재생합니다.
        /// </summary>
        private void PlayInternal(Vector3 position, PotionSize size)
        {
            if (_player == null)
            {
                return;
            }

            float intensity = GetSizeIntensity(size) * GetFalloff(position);

            if (intensity < _minIntensity)
            {
                return;
            }

            // 짧은 시간 안에 더 약한 폭발이 또 터지면 무시합니다. (후처리 연출이 겹쳐서 과해지는 것을 막음)
            float now = Time.unscaledTime;
            if (now - _lastPlayTime < _overlapWindowSeconds && intensity <= _lastIntensity)
            {
                return;
            }

            _lastPlayTime = now;
            _lastIntensity = intensity;

            _player.PlayFeedbacks(position, intensity);
        }

        private float GetSizeIntensity(PotionSize size)
        {
            switch (size)
            {
                case PotionSize.Small:
                    return _smallIntensity;
                case PotionSize.Medium:
                    return _mediumIntensity;
                case PotionSize.Large:
                    return _largeIntensity;
                default:
                    return _mediumIntensity;
            }
        }

        /// <summary>
        /// 내 카메라와 폭발 위치의 거리에 따른 세기 배율(0~1)입니다. 카메라가 없으면 감쇠 없이 1 입니다.
        /// </summary>
        private float GetFalloff(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return 1f;
            }

            float distance = Vector3.Distance(camera.transform.position, position);
            if (distance >= _maxDistance)
            {
                return 0f;
            }

            return Mathf.Clamp01(_distanceFalloff.Evaluate(distance / _maxDistance));
        }

#if UNITY_EDITOR
        // ==================== 에디터 테스트용 ============================

        [Button("테스트: 이 위치에서 재생", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void PlayForTest(PotionSize size = PotionSize.Medium)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ExplosionFeedbackHub] : Play 모드에서만 테스트할 수 있습니다.");
                return;
            }

            PlayInternal(transform.position, size);
        }
#endif
    }
}
