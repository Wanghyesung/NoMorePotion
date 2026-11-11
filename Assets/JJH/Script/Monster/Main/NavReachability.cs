using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

using SF = UnityEngine.SerializeField;

/// <summary>
/// NavMesh 상에서 목표 지점까지 실제로 갈 수 있는지 판정
/// </summary>
public static class NavReachability
{
    private static NavMeshPath path;

    public static bool IsReachable(Vector3 from, Vector3 to, float sampleRadius = 2f)
    {
        path ??= new NavMeshPath();

        // 타겟 근처에 NavMesh 자체가 없으면 도달 불가
        if (!NavMesh.SamplePosition(to, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return false;

        if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path))
            return false;

        // PathPartial = 중간까지만 갈 수 있음 → 도달 불가로 취급
        return path.status == NavMeshPathStatus.PathComplete;
    }
}

/// <summary>
/// 도달 불가 상태가 유예 시간 이상 지속되면 추적 해제를 알리고,
/// 해제된 타겟은 일정 시간 다시 잡지 못하게 막음
/// </summary>
[Serializable]
public class ReachabilityTracker
{
    // 경로 계산 주기
    [SF] private float checkInterval = 0.5f;     
    // 도달 해제 제한 시간
    [SF] private float graceTime = 2.5f;
    // 해제 후 같은 타겟 재추적 금지
    [SF] private float reacquireCooldown = 3f;
    [SF] private float sampleRadius = 2f;

    private GameObject trackedTarget;
    private float checkTimer;
    private float unreachableTime;

    private readonly Dictionary<GameObject, float> blockedUntil = new();

    /// <summary>경계 유예 여부</summary>
    public bool IsInGrace => unreachableTime > 0f;

    /// <summary>
    /// 부활 시 간헐적 플레이어 무시 방지 명시적 초기화
    /// </summary>
    public void ResetState()
    {
        trackedTarget = null;
        checkTimer = 0f;
        unreachableTime = 0f;
        blockedUntil.Clear();
    }

    /// <summary>
    /// 타겟이 추적 가능 대상에서 벗어난 경우 true 반환
    /// </summary>
    /// <param name="from"></param>
    /// <param name="target"></param>
    /// <param name="deltaTime"></param>
    /// <returns>추적 가능 여부</returns>
    public bool ShouldDrop(Vector3 from, GameObject target, float deltaTime)
    {
        if (target != trackedTarget)
        {
            trackedTarget = target;
            checkTimer = 0f;
            unreachableTime = 0f;
        }

        checkTimer += deltaTime;
        if (checkTimer < checkInterval) return false;

        float elapsed = checkTimer;
        checkTimer = 0f;

        if (NavReachability.IsReachable(from, target.transform.position, sampleRadius))
        {
            unreachableTime = 0f;
            return false;
        }

        unreachableTime += elapsed;
        if (unreachableTime < graceTime) return false;

        blockedUntil[target] = Time.time + reacquireCooldown;
        trackedTarget = null;
        unreachableTime = 0f;
        return true;
    }

    /// <summary>
    /// 재추적 가능 여부
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public bool CanAcquire(GameObject target)
    {
        if (target == null) return false;
        return !blockedUntil.TryGetValue(target, out float until) || Time.time >= until;
    }
}
