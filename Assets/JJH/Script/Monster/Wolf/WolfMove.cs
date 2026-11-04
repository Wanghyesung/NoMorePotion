using UnityEngine;
using static JJH_Enums;

public class WolfMove : MonsterMove
{
    public WolfPack Pack { get; private set; }
    public bool IsAlpha => Pack != null && Pack.Alpha == this;

    // new 필드 대신 캐스팅. 부모 Awake가 채운 anim을 그대로 씀
    public WolfAnamationController WolfAnim => anim as WolfAnamationController;

    public void JoinPack(WolfPack pack) => Pack = pack;

    protected override void RegisterStates()
    {
        stateGroup[MonsterState.Follow] = new WolfFollowState(this);
        stateGroup[MonsterState.Encircle] = new WolfEncircleState(this);
        stateGroup[MonsterState.Pound] = new WolfPoundState(this);
    }

    protected override IMonsterState UpdateState()
    {
        // 무리 없음 or 알파 사망 → 일반 몬스터처럼
        if (Pack == null || !Pack.IsActive) return base.UpdateState();
        if (state.GetStateType() == MonsterState.Spawn) return null;

        MonsterState next;

        switch (Pack.Phase)
        {
            case PackPhase.Patrol:
                if (IsAlpha) return base.UpdateState();   // 기존 순찰/대기 그대로
                next = MonsterState.Follow;
                break;

            case PackPhase.Encircle:
                next = MonsterState.Encircle;
                break;

            case PackPhase.Pound:
                next = IsAlpha ? MonsterState.Pound : MonsterState.Encircle;
                break;

            default: // Assault: 기존 Chase / Attack 재사용
                if (nowTarget == null) return base.UpdateState();
                float dist = Vector3.Distance(transform.position, nowTarget.transform.position);
                next = dist <= AttackRange ? MonsterState.Attack : MonsterState.Chase;
                break;
        }

        return state.GetStateType() == next ? null : stateGroup[next];
    }

    protected override bool ShouldFaceTarget()
    {
        // Pound 사거리 도착해서 멈췄을 때도 타겟 쪽으로 회전
        return base.ShouldFaceTarget()
            || (state.GetStateType() == MonsterState.Pound && !agent.hasPath);
    }

}
