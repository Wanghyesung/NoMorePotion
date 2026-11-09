using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 월드 오브젝트(꽃, 나무, 광물 등)가 맞았을 때와 소진되었을 때의 "오브젝트 쪽" 연출을 재생하는 중계 컴포넌트입니다.
    /// WorldObject 가 있는 오브젝트에 붙이고, WorldObject 의 OnCountChanged / OnDepleted 이벤트만 구독합니다.
    /// 멀티플레이에서는 마스터가 확정한 횟수가 모든 클라이언트에 반영될 때 이벤트가 발생하므로, 모두에게 같은 연출이 보입니다.
    ///
    /// - 맞았을 때 : 횟수가 줄어들었을 때(소진되는 마지막 한 번은 제외) CharacterHitFeedback 을 재생합니다.
    ///   세기는 "줄어든 횟수 / 최대 횟수"입니다. 세기 구간은 CharacterHitFeedback 의 기준값으로 오브젝트 종류마다 조절합니다.
    /// - 소진되었을 때 : 연결한 MMF_Player(터지는 연출, 파편 등)를 한 번 재생합니다. 마지막 한 번의 피격은 이쪽이 맡습니다.
    /// - 횟수가 늘어나는 변화(시작할 때의 채움, 복구)에는 재생하지 않습니다.
    ///
    /// 소진된 오브젝트를 숨기거나 없애는 것은 이 컴포넌트가 하지 않습니다. (WorldObject 는 소진 사실만 알립니다)
    /// </summary>
    [RequireComponent(typeof(WorldObject))]
    public class WorldObjectFeedbackRelay : MonoBehaviour
    {
        [SerializeField, LabelText("맞을 때 연출 (찌그러짐 등)"), Required("CharacterHitFeedback 을 연결해 주세요.")]
        private CharacterHitFeedback _hitFeedback;

        [SerializeField, LabelText("소진될 때 연출 (선택)")]
        private MMF_Player _depletedPlayer;

        private WorldObject _worldObject;

        private void Awake()
        {
            _worldObject = GetComponent<WorldObject>();
        }

        private void OnEnable()
        {
            if (_worldObject == null)
            {
                _worldObject = GetComponent<WorldObject>();
            }

            _worldObject.OnCountChanged += HandleCountChanged;
            _worldObject.OnDepleted += HandleDepleted;
        }

        private void OnDisable()
        {
            if (_worldObject != null)
            {
                _worldObject.OnCountChanged -= HandleCountChanged;
                _worldObject.OnDepleted -= HandleDepleted;
            }
        }

        private void HandleCountChanged(int previous, int current)
        {
            // 횟수가 줄어든 경우만 "맞은 것"입니다. 마지막 한 번(0 이 됨)은 소진 연출이 맡습니다.
            if (current >= previous || current <= 0 || _hitFeedback == null)
            {
                return;
            }

            int maxCount = _worldObject.data != null ? _worldObject.data.maxCount : 0;
            if (maxCount <= 0)
            {
                return;
            }

            _hitFeedback.Play((previous - current) / (float)maxCount);
        }

        private void HandleDepleted(WorldObject worldObject)
        {
            if (_depletedPlayer != null)
            {
                _depletedPlayer.PlayFeedbacks(transform.position);
            }
        }
    }
}
