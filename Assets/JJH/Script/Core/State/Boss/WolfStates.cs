using UnityEngine;
using static JJH_Enums;

/// <summary>
/// WolfStateBase 전용 유사 HFSM(상위 FSM와 하위 개체의 2계층 FSM 구조)
/// 늑대 분대 AI 전용
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
    private const float ArriveDistance = 1f;
    private const float CatchUpDistance = 4f;
    private bool returning;   // 대형 위치 복귀 중?
    private float leash;      // 자유 반경

    public WolfFollowState(WolfMove wolf) : base(wolf) { }
    public override MonsterState GetStateType() => MonsterState.Follow;

    public override void OnStart()
    {
        wolf.agent.isStopped = false;
        returning = false;
        leash = wolf.Pack.FollowLeash + Random.Range(-0.7f, 0.7f);
    }

    public override void OnStay()
    {
        float alphaDist = Vector3.Distance(wolf.transform.position, wolf.Pack.Alpha.transform.position);

        if (!returning && alphaDist > leash)
            returning = true;

        if (!returning)
        {
            Stop();
            return;
        }

        Vector3 dest = wolf.Pack.GetFollowPoint(wolf);
        float slotDist = Vector3.Distance(wolf.transform.position, dest);

        if (slotDist <= ArriveDistance)
        {
            returning = false;
            Stop();
            return;
        }

        bool farBehind = slotDist > CatchUpDistance;

        wolf.agent.speed = farBehind ? wolf.runSpeed : wolf.walkSpeed;
        wolf.anim.IsRun(farBehind);
        wolf.anim.IsWalk(!farBehind);
        wolf.agent.SetDestination(dest);

    }

    public override void OnEnd()
    {
        wolf.anim.IsWalk(false);
        wolf.anim.IsRun(false);
    }

    private void Stop()
    {
        wolf.agent.ResetPath();
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