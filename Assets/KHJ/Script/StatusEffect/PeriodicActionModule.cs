using System;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 주기적으로 발동하는 행동의 종류를 정의하는 열거형입니다.
    /// StopPlayerMove : 플레이어 이동 정지 / ConsumeFirstItem : 인벤토리 첫 번째 아이템 소모 / DrinkHeldPotion : 들고 있는 물약 마시기
    /// </summary>
    public enum PeriodicActionType
    {
        None,
        StopPlayerMove,
        ConsumeFirstItem,
        DrinkHeldPotion,
    }

    /// <summary>
    /// 일정 간격으로 행동을 발동시키는 모듈입니다. 발동 시점(타이밍)만 BuffContainer 가 관리하고,
    /// 실제 행동은 BuffContainer 의 OnPeriodicAction 이벤트를 받는 쪽(플레이어, 인벤토리 등)이 처리합니다.
    /// 걸리자마자 한 번 발동하고, 이후 반복 간격마다 발동합니다. 간격이 0 이면 한 번만 발동합니다.
    /// 예) 추위 : StopPlayerMove 5초마다 / 허기 : ConsumeFirstItem 3초마다 / 갈증 : DrinkHeldPotion 5초마다
    /// </summary>
    [Serializable]
    public class PeriodicActionModule : StatusEffectModule
    {
        [SerializeField, LabelText("발동 행동")]
        private PeriodicActionType _actionType = PeriodicActionType.None;
        public PeriodicActionType ActionType => _actionType;

        [SerializeField, LabelText("반복 간격 (초, 0 = 한 번)"), MinValue(0)]
        private float _intervalSeconds = 0f;
        public float IntervalSeconds => _intervalSeconds;

        // 이동 정지 행동에서 한 번 발동할 때 이동을 막는 시간입니다.
        [SerializeField, LabelText("정지 시간 (초)"), MinValue(0), ShowIf("_actionType", PeriodicActionType.StopPlayerMove)]
        private float _durationSeconds = 0f;
        public float DurationSeconds => _durationSeconds;

        // 아이템 소모 행동에서 소모에 성공했을 때 채우는 포만감입니다. (0~100 범위 상태라 퍼센트와 수치가 같음)
        [SerializeField, LabelText("포만감 충전량"), MinValue(0), ShowIf("_actionType", PeriodicActionType.ConsumeFirstItem)]
        private int _amount = 0;
        public int Amount => _amount;
    }
}
