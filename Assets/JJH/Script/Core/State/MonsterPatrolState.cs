using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class MonsterPatrolState : IMonsterState
{
    private MonsterMove mover;

    public MonsterPatrolState(MonsterMove mover)
    {
        this.mover = mover;
    }

    public void OnEnd()
    {
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
                moverAI.SetDestination(newRandomPos);
            }
        }
    }

    public void OnStay()
    {
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
