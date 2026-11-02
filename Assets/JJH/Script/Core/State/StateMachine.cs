using UnityEngine;
using static JJH_Enums;
public class StateMachine
{
    private IMonsterState nowState;
    private IMonsterState beforeState;

    /// <summary>
    /// 상태를 전환하는 함수
    /// </summary>
    /// <param name="nextState"></param>
    public void ChangeState(IMonsterState nextState)
    {
        if (nowState == null)
        {
            nowState = nextState;
            nowState?.OnStart();
            return;
        }

        nowState.OnEnd();
        beforeState = nowState;
        nowState = nextState;
        nextState.OnStart();
    }

    public void Stay()
    {
        nowState?.OnStay();
    }

    /// <summary>
    /// 현재 / 이전 상태가 무엇이었는지 가져오는 함수
    /// </summary>
    /// <param name="now">현재 = true / 이전 = false</param>
    /// <returns>호출된 지점의 상태값</returns>
    public MonsterState GetStateType(bool now = true)
    {
        IMonsterState callState = now ? nowState : beforeState;

        if (callState is MonsterPatrolState)
            return MonsterState.Patrol;

        else if (callState is MonsterChaseState)
            return MonsterState.Chase;

        else if (callState is MonsterAttackState)
            return MonsterState.Attack;
        else
            return MonsterState.None;
    }
}