using UnityEngine;

/*///////////////////////////////////////////
                    SOHitState
기능 : 상체 피격 클립을 설정하고 런타임 상태 생성
 *///////////////////////////////////////////

[CreateAssetMenu(fileName = "Hit", menuName = "Player/State/Hit")]
public sealed class SOHitState : SOState
{
    [SerializeField, Min(0f)] private float knockbackDeceleration = 12f;

    public override FSM.StateType State => FSM.StateType.Hit;
    public override PlayerState CreateState(FSM owner)
        => new PlayerHitState(owner, this, knockbackDeceleration);
}
