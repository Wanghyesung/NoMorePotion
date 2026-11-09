using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 캐릭터(몬스터, 플레이어) 한 명의 "몸" 피격 연출을 재생합니다. 늘어났다 줄어드는 찌그러짐(Squash and Stretch), 깜빡임(Blink) 등.
    /// 화면 연출(카메라, 후처리)은 씬의 허브가 맡고, 이 컴포넌트는 그 캐릭터의 몸만 다룹니다.
    ///
    /// 사용법
    /// - 캐릭터 프리팹에 이 컴포넌트와 자식 MMF_Player 를 두고, 모델 루트와 깜빡일 대상을 연결합니다.
    /// - MMF_Player 에 MMF_SquashAndStretch / MMF_Blink 를 추가하면, 대상이 비어 있는 피드백은 아래 연결값으로 자동으로 채웁니다.
    /// - 피격을 알리는 쪽은 Play(damageRatio) 를 호출합니다. (damageRatio = 받은 피해 / 최대 체력)
    ///
    /// 주의 : 찌그러짐 대상(모델 루트)은 콜라이더, NavMeshAgent, Rigidbody 가 붙은 루트가 아니라 그 아래의 모델이어야 합니다.
    /// 스케일이 (1, 1, 1) 로 정규화되어 있어야 하고, 애니메이터가 그 스케일을 직접 바꾸면 안 됩니다.
    /// </summary>
    public class CharacterHitFeedback : MonoBehaviour
    {
        [SerializeField, LabelText("피격 연출 플레이어"), Required("재생할 MMF_Player 를 연결해 주세요.")]
        private MMF_Player _player;

        [SerializeField, LabelText("찌그러짐 대상 (모델 루트)"), BoxGroup("자동 연결")]
        private Transform _visualRoot;

        [SerializeField, LabelText("깜빡임 대상"), BoxGroup("자동 연결")]
        private MMBlink _blink;

        [SerializeField, LabelText("약한 피격 세기"), BoxGroup("세기"), MinValue(0f)]
        private float _lightIntensity = 0.5f;

        [SerializeField, LabelText("강한 피격 세기"), BoxGroup("세기"), MinValue(0f)]
        private float _heavyIntensity = 1.5f;

        // 이 비율(받은 피해 / 최대 체력) 이하는 약한 피격 세기, 이상은 강한 피격 세기이고 그 사이는 보간합니다.
        [SerializeField, LabelText("약한 피격 기준 (최대 체력 대비)"), BoxGroup("세기"), Range(0f, 1f)]
        private float _lightRatio = 0.02f;

        [SerializeField, LabelText("강한 피격 기준 (최대 체력 대비)"), BoxGroup("세기"), Range(0f, 1f)]
        private float _heavyRatio = 0.3f;

        [SerializeField, LabelText("재생 간격 (초)"), BoxGroup("겹침 제어"), MinValue(0f)]
        private float _cooldownSeconds = 0.1f;

        private float _lastPlayTime = float.NegativeInfinity;

        private void Awake()
        {
            WireTargets();
        }

        private void OnDisable()
        {
            // 풀링되거나 비활성화될 때 늘어난 채로 남지 않도록 멈추고 되돌립니다.
            if (_player != null)
            {
                _player.StopFeedbacks();
                _player.ResetFeedbacks();
            }
        }

        /// <summary>
        /// 피격 연출을 재생합니다.
        /// </summary>
        /// <param name="damageRatio">받은 피해 / 최대 체력</param>
        public void Play(float damageRatio)
        {
            if (_player == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastPlayTime < _cooldownSeconds)
            {
                return;
            }

            _lastPlayTime = now;

            float t = Mathf.InverseLerp(_lightRatio, _heavyRatio, damageRatio);
            float intensity = Mathf.Lerp(_lightIntensity, _heavyIntensity, t);

            _player.PlayFeedbacks(transform.position, intensity);
        }

        /// <summary>
        /// MMF_Player 안의 피드백 중 대상이 비어 있는 것을 이 컴포넌트의 연결값으로 채웁니다.
        /// 캐릭터마다 피드백 대상을 손으로 연결하지 않아도 되게 하려는 것입니다. 이미 연결된 대상은 건드리지 않습니다.
        /// </summary>
        private void WireTargets()
        {
            if (_player == null || _player.FeedbacksList == null)
            {
                return;
            }

            for (int i = 0; i < _player.FeedbacksList.Count; i++)
            {
                MMF_Feedback feedback = _player.FeedbacksList[i];

                if (feedback is MMF_SquashAndStretch squash && squash.SquashAndStretchTarget == null)
                {
                    squash.SquashAndStretchTarget = _visualRoot;
                }
                else if (feedback is MMF_Blink blink && blink.TargetBlink == null)
                {
                    blink.TargetBlink = _blink;
                }
            }
        }

#if UNITY_EDITOR
        // ==================== 에디터 테스트용 ============================

        [Button("테스트: 피격 재생", ButtonSizes.Large)]
        [GUIColor(1f, 0.8f, 0.4f)]
        private void PlayForTest(float damageRatio = 0.1f)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[CharacterHitFeedback] : Play 모드에서만 테스트할 수 있습니다.");
                return;
            }

            Play(damageRatio);
        }
#endif
    }
}
