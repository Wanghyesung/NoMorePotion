using System.Collections.Generic;
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
    /// 찌그러짐 세기 : Feel 은 플레이어의 세기를 Remap Curve One 에 그대로 곱하는데, Absolute 모드(Zero 1, One 0.75 처럼 1 근처 값)에서는
    /// 곱하면 방향이 뒤집힙니다. (세기 1.5 → 1.125 = 눌리지 않고 늘어남) 그래서 찌그러짐 피드백은 Feel 의 세기 곱셈을 끄고(Constant Intensity),
    /// 이 컴포넌트가 "Zero 에서 One 까지의 변화량"에 세기를 곱한 값으로 Remap Curve One 을 매번 정해서 넘깁니다.
    /// 결과적으로 인스펙터의 One 값은 "세기 1 일 때의 최종 스케일"이고, 세기 0.5 는 변화량의 절반만 눌립니다. (Additive 모드도 같은 식으로 동작)
    ///
    /// 주의 : 찌그러짐 대상(모델 루트)은 콜라이더, NavMeshAgent, Rigidbody 가 붙은 루트가 아니라 그 아래의 모델이어야 합니다.
    /// Absolute 모드는 스케일을 절대값으로 쓰므로 대상의 스케일이 (1, 1, 1) 이어야 하고, 애니메이터가 그 스케일을 직접 바꾸면 안 됩니다.
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

        // 찌그러짐 피드백과, 인스펙터에 설정된 Remap Curve One 의 원래 값입니다. 재생할 때마다 세기를 반영해 다시 계산합니다.
        private readonly List<SquashEntry> _squashEntries = new List<SquashEntry>();

        private struct SquashEntry
        {
            public MMF_SquashAndStretch Feedback;
            public float BaseRemapOne;
        }

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

            // 찌그러짐은 Feel 이 세기를 곱하지 않으므로, 변화량(Zero → One)에 세기를 곱한 값을 직접 넣습니다.
            for (int i = 0; i < _squashEntries.Count; i++)
            {
                SquashEntry entry = _squashEntries[i];
                float zero = entry.Feedback.RemapCurveZero;
                entry.Feedback.RemapCurveOne = zero + (entry.BaseRemapOne - zero) * intensity;
            }

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

                if (feedback is MMF_SquashAndStretch squash)
                {
                    if (squash.SquashAndStretchTarget == null)
                    {
                        squash.SquashAndStretchTarget = _visualRoot;
                    }

                    // Feel 의 세기 곱셈을 끄고(세기는 Play 에서 직접 반영), 인스펙터의 원래 One 값을 기억해 둡니다.
                    squash.Timing.ConstantIntensity = true;
                    _squashEntries.Add(new SquashEntry { Feedback = squash, BaseRemapOne = squash.RemapCurveOne });
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
