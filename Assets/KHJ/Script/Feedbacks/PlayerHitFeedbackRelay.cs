using Photon.Pun;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 플레이어가 피격당하면 PlayerHitFeedbackHub 에 알려 주는 중계 컴포넌트입니다. 플레이어 오브젝트에 붙입니다.
    /// 플레이어 코드는 건드리지 않고, 공개된 FSM.OnHitAction 이벤트만 구독합니다.
    ///
    /// - 몸 연출(CharacterHitFeedback)은 누구의 피격이든 모두에게 보이도록 재생합니다. (같은 오브젝트나 자식에 있을 때만)
    /// - 화면 연출(PlayerHitFeedbackHub)은 내 플레이어일 때만 재생합니다. (PhotonView 가 있고 내 것이 아니면 다른 사람의 피격이라 무시)
    /// - 마지막 일격처럼 HP 가 0 이 되는 피격은 FSM 이 사망 상태로 보내서 이 이벤트가 오지 않습니다. 사망 연출은 따로 필요합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerHitFeedbackRelay : MonoBehaviour
    {
        private FSM _fsm;
        private Player _player;
        private PhotonView _photonView;
        private CharacterHitFeedback _bodyFeedback;

        private void Awake()
        {
            _fsm = GetComponent<FSM>();
            _player = GetComponent<Player>();
            _photonView = GetComponent<PhotonView>();
            _bodyFeedback = GetComponentInChildren<CharacterHitFeedback>();

            if (_fsm == null || _player == null)
            {
                Debug.LogWarning("[PlayerHitFeedbackRelay] : 같은 오브젝트에 FSM 과 Player 가 있어야 합니다.");
            }
        }

        private void OnEnable()
        {
            if (_fsm != null)
            {
                _fsm.OnHitAction += HandleHit;
            }
        }

        private void OnDisable()
        {
            if (_fsm != null)
            {
                _fsm.OnHitAction -= HandleHit;
            }
        }

        private void HandleHit(CommonStruct.AttackInfo attackInfo)
        {
            Character character = _player != null ? _player.Character : null;
            if (character == null || character.MaxHP <= 0)
            {
                return;
            }

            float damageRatio = attackInfo.Damage / (float)character.MaxHP;

            // 몸 연출은 누구의 피격이든 보입니다.
            if (_bodyFeedback != null)
            {
                _bodyFeedback.Play(damageRatio);
            }

            // 다른 플레이어의 피격은 내 화면에 연출하지 않습니다.
            if (_photonView != null && !_photonView.IsMine)
            {
                return;
            }

            PlayerHitFeedbackHub.Play(damageRatio);
        }
    }
}
