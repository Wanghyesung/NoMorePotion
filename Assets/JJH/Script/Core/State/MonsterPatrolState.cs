using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using static JJH_Enums;

/// <summary>
/// 몬스터 순찰 상태 FSM
/// </summary>
public class MonsterPatrolState : IMonsterState
{
    private MonsterMove mover;
    private const float StartGrace = 0.3f;
    private float startTime;
    public MonsterPatrolState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnEnd()
    {
        mover.patrolTimerActive = true;
        mover.patrolTimer = 0;
        mover.anim.IsWalk(false);
    }

    public void OnStart()
    {
        NavMeshAgent moverAI = mover.agent;
        moverAI.isStopped = false;
        
        if (!moverAI.pathPending && moverAI.remainingDistance <= moverAI.stoppingDistance)
        {
            Vector3 newRandomPos = 
                GetRandomNavMeshPosition(mover.transform.position, mover.nextMoveDist);

            // 유효한 좌표를 찾았다면 이동 명령 수행
            if (newRandomPos != Vector3.zero)
            {
                moverAI.speed = mover.walkSpeed;

                moverAI.SetDestination(newRandomPos);
                // OnStart에서 SetPath 직후
                startTime = Time.time;
                mover.anim.IsWalk();
                mover.anim.IsRun(false);
            }
        }
    }

    public void OnStay()
    {
        if (IsMoverStop())
            mover.state.ChangeState(mover.stateGroup[MonsterState.Idle]);
        
    }

    /// <summary>
    /// 몬스터의 AI가 이동을 멈춘 상태인지 확인하는 함수
    /// </summary>
    /// <returns></returns>
    private bool IsMoverStop()
    {
        NavMeshAgent moverAI = mover.agent;
        // 정지 명령 상태인 경우
        if (moverAI.isStopped) return true;
        // 길찾기중인 경우
        if (moverAI.pathPending) return false;

        // 출발 직후 보류
        if (Time.time - startTime < StartGrace) return false;

        // 정지거리가 현재거리보다 긴 경우, 
        if (moverAI.remainingDistance <= moverAI.stoppingDistance)
        {
            // 경로가 없거나 속도가 0에 가까운 경우 
            if (!moverAI.hasPath || moverAI.velocity.sqrMagnitude < 0.01f)
                return true;
        }

        // 목적지는 여전히 멀지만 어떠한 경우로던 멈춰진 경우
        if (moverAI.velocity.sqrMagnitude < 0.001f && 
            moverAI.remainingDistance > moverAI.stoppingDistance)
            return true;

        return false;
    }

    Vector3 GetRandomNavMeshPosition(Vector3 center, float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += center;


        // 랜덤 좌표에서 가장 가까운 이동 가능한 매시맵 추적
        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, 
            radius, NavMesh.AllAreas))
            return hit.position;


        return Vector3.zero;
    }
}
