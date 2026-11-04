using UnityEngine;
using static JJH_Enums;

/// <summary>
/// IMonsterState(OnStart / OnStay / OnEnd) 구현 베이스
/// </summary>
public abstract class WolfStateBase : IMonsterState
{
    protected readonly WolfMove wolf;
    protected WolfStateBase(WolfMove wolf) => this.wolf = wolf;

    public abstract MonsterState GetStateType();
    public virtual void OnStart() { }
    public virtual void OnStay() { }
    public virtual void OnEnd() { }
}

/// <summary>
/// 일반 늑대: 순찰 중 알파 뒤 대형 유지
/// </summary>
public class WolfFollowState : WolfStateBase
{
    private const float StopDistance = 0.5f;
    private const float CatchUpDistance = 4f;

    public WolfFollowState(WolfMove wolf) : base(wolf) { }
    public override MonsterState GetStateType() => MonsterState.Follow;

    public override void OnStart()
    {
        wolf.agent.isStopped = false;
    }

    public override void OnStay()
    {
        Vector3 dest = wolf.Pack.GetFollowPoint(wolf);
        float dist = Vector3.Distance(wolf.transform.position, dest);

        bool moving = dist > StopDistance;
        bool farBehind = dist > CatchUpDistance;

        wolf.agent.speed = farBehind ? wolf.runSpeed : wolf.walkSpeed;
        wolf.anim.IsRun(moving && farBehind);
        wolf.anim.IsWalk(moving && !farBehind);

        if (moving) wolf.agent.SetDestination(dest);
        else wolf.agent.ResetPath();
    }

    public override void OnEnd()
    {
        wolf.anim.IsWalk(false);
        wolf.anim.IsRun(false);
    }
}

/// <summary>
/// 타겟 주변 선회 상태
/// </summary>
public class WolfEncircleState : WolfStateBase
{
    public WolfEncircleState(WolfMove wolf) : base(wolf) { }
    public override MonsterState GetStateType() => MonsterState.Encircle;

    public override void OnStart()
    {
        wolf.agent.isStopped = false;
        wolf.agent.speed = wolf.runSpeed;
        wolf.anim.IsWalk(false);
        wolf.anim.IsRun(true);
    }

    public override void OnStay()
    {
        wolf.agent.SetDestination(wolf.Pack.GetEncirclePoint(wolf));
    }

    public override void OnEnd()
    {
        wolf.anim.IsRun(false);
    }
}

/// <summary>
/// 알파 전용 클래스
/// <para>사거리까지 접근 → 정지 후 타겟 방향 정렬 → Pound</para>
/// </summary>
public class WolfPoundState : WolfStateBase
{
    private const float FaceAngle = 15f;
    private bool started;

    public WolfPoundState(WolfMove wolf) : base(wolf) { }
    public override MonsterState GetStateType() => MonsterState.Pound;

    public override void OnStart()
    {
        started = false;
        wolf.agent.isStopped = false;
        wolf.agent.speed = wolf.runSpeed;
        wolf.anim.IsWalk(false);
        wolf.anim.IsRun(true);
    }

    public override void OnStay()
    {
        if (started || wolf.nowTarget == null) return;

        Vector3 toTarget = wolf.nowTarget.transform.position - wolf.transform.position;
        toTarget.y = 0;

        if (toTarget.magnitude > wolf.Pack.PoundRange)
        {
            wolf.anim.IsRun(true);
            wolf.agent.SetDestination(wolf.nowTarget.transform.position);
            return;
        }

        wolf.agent.ResetPath();
        wolf.anim.IsRun(false);

        if (Vector3.Angle(wolf.transform.forward, toTarget) <= FaceAngle)
        {
            wolf.WolfAnim.IsPound(true);
            started = true;
        }
    }

    public override void OnEnd()
    {
        wolf.anim.IsRun(false);
        wolf.WolfAnim.IsPound(false);
    }
}