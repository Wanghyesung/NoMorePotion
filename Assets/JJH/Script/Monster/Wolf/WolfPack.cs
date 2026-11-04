using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static JJH_Enums;

using SF = UnityEngine.SerializeField;

public enum PackPhase
{
    Patrol,     // 알파 순찰, 노말은 대형 유지하며 추적
    Encircle,   // 타겟 주변 선회
    Pound,      // 알파만 땅찍기, 노말은 선회 유지
    Assault     // Pound 쿨타임 동안 물기 공격
}

public class WolfPack : MonoBehaviour
{
    [Header("늑대 데이터")]
    [SF] private WolfMove alpha;
    [SF] private List<WolfMove> normals = new();

    [Header("일반 늑대 무리 대형")]
    [SF] private Vector3[] formationOffsets =
    {
        new (-1.5f, 0f, -1.5f),
        new ( 1.5f, 0f, -1.5f),
    };

    [Header("선회")]
    [SF] private float turnRadius = 5f;
    [SF] private float turnSpeed = 40f;
    [SF] private float poundDelay = 4f;

    [Header("Pound")]
    [SF] private float poundRange = 3f;
    [SF] private float poundCooldown = 20f;
    [SF] private float poundTimeout = 5f;

    [Header("타겟")]
    [SF] private float loseDistance = 15f;      // 전원이 이 거리 밖이면 타겟 해제

    public PackPhase Phase { get; private set; } = PackPhase.Patrol;
    public GameObject Target { get; private set; }
    public WolfMove Alpha => alpha;
    public float PoundRange => poundRange;
    public bool IsActive => IsAlive(alpha);

    private readonly List<WolfMove> members = new();
    private readonly List<WolfMove> alive = new();
    private float phaseTimer;
    private float turnAngle;

    private void Awake()
    {
        members.Add(alpha);
        members.AddRange(normals);

        foreach (WolfMove mover in members)
            mover.JoinPack(this);
    }

    private void Update()
    {
        // 포톤 사용하면 if (!PhotonNetwork.IsMasterClient) return;
        RefreshAlive();

        // 알파 사망 시 로직 해제, 노말은 기본 MonsterMove 로직으로 동작
        if (!IsActive)
        {
            enabled = false;
            return;
        }

        RefreshTarget();

        if (Target == null && Phase != PackPhase.Patrol)
            ChangePhase(PackPhase.Patrol);

        phaseTimer += Time.deltaTime;
        turnAngle += turnSpeed * Time.deltaTime;

        switch (Phase)
        {
            case PackPhase.Patrol:
                if (Target != null) ChangePhase(PackPhase.Encircle);
                break;

            case PackPhase.Encircle:
                if (phaseTimer >= poundDelay) ChangePhase(PackPhase.Pound);
                break;

            case PackPhase.Pound:
                if (phaseTimer >= poundTimeout) ChangePhase(PackPhase.Assault);
                break;

            case PackPhase.Assault:
                if (phaseTimer >= poundCooldown) ChangePhase(PackPhase.Encircle);
                break;
        }
    }

    private void ChangePhase(PackPhase next)
    {
        Phase = next;
        phaseTimer = 0;

        // 선회 각도 조절
        if (next == PackPhase.Encircle && Target != null)
        {
            Vector3 dir = alpha.transform.position - Target.transform.position;
            turnAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
    }

    #region 외부 호출
    /// <summary>
    /// WolfCombat Pound 애니메이션 이벤트 종료 호출 함수
    /// </summary>
    public void OnPoundFinished()
    {
        if (Phase == PackPhase.Pound)
            ChangePhase(PackPhase.Assault);
    }

    public Vector3 GetEncirclePoint(WolfMove wolf)
    {
        if (Target == null || alive.Count == 0)
            return wolf.transform.position;

        int idx = Mathf.Max(0, alive.IndexOf(wolf));
        float angle = turnAngle + idx * 360f / alive.Count;
        Vector3 offset = Quaternion.Euler(0, angle, 0) * Vector3.forward * turnRadius;

        return SampleNav(Target.transform.position + offset);
    }

    public Vector3 GetFollowPoint(WolfMove wolf)
    {
        int idx = normals.IndexOf(wolf);
        Vector3 local = formationOffsets.Length > 0
            ? formationOffsets[Mathf.Clamp(idx, 0, formationOffsets.Length - 1)]
            : Vector3.back * 2f;

        // 회전 적용
        return SampleNav(alpha.transform.position + alpha.transform.rotation * local);
    }
    #endregion

    #region 내부
    private void RefreshAlive()
    {
        alive.Clear();
        foreach (WolfMove m in members)
            if (IsAlive(m)) alive.Add(m);
    }

    private void RefreshTarget()
    {
        if (Target != null && !IsTargetValid(Target))
        {
            Target = null;
            foreach (WolfMove m in alive)
                m.nowTarget = null;
        }

        // 늑대 3마리중 1마리가 적을 감지할 경우 모두 타겟 적용
        if (Target == null)
        {
            foreach (WolfMove m in alive)
            {
                if (m.nowTarget != null && m.nowTarget.activeInHierarchy)
                {
                    Target = m.nowTarget;
                    break;
                }
            }
        }

        foreach (WolfMove m in alive)
            m.nowTarget = Target;
    }

    private bool IsTargetValid(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;

        float sqr = loseDistance * loseDistance;
        foreach (WolfMove m in alive)
            if ((m.transform.position - target.transform.position).sqrMagnitude <= sqr)
                return true;

        return false;
    }

    private static bool IsAlive(WolfMove m)
    {
        if (m == null || !m.isActiveAndEnabled) return false;
        return m.state == null || m.state.GetStateType() != MonsterState.Death;
    }

    private static Vector3 SampleNav(Vector3 point)
    {
        return NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position
            : point;
    }
    #endregion

    private void OnDrawGizmosSelected()
    {
        if (Target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Target.transform.position, turnRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Target.transform.position, poundRange);
    }
}
