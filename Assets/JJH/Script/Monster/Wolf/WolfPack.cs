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
    [SF] public int alphaId;
    [SF] private List<WolfMove> normals = new();

    [Header("일반 늑대 무리 대형")]
    [SF] private Vector3[] formationOffsets =
    {
        new (-1.5f, 0f, -1.5f),
        new ( 1.5f, 0f, -1.5f),
    };
    [SF] private float followLeash = 4f;

    [Header("선회 관련 값")]
    [SF] private float turnRadius = 5f;
    [SF] private float turnSpeed = 40f;
    [SF] private float poundDelay = 4f;

    [Header("땅찍기 관련 값")]
    [SF] private float poundRange = 3f;
    [SF] private float poundCooldown = 15f;
    [SF] private float poundTimeout = 5f;

    [Header("타겟 관련 값")]
    [SF] private float loseDistance = 15f;      // 전원이 이 거리 밖이면 타겟 해제
    [SF] private ReachabilityTracker reachability = new();
    public PackPhase Phase { get; private set; } = PackPhase.Patrol;
    public GameObject Target { get; private set; }
    public WolfMove Alpha => alpha;
    public float FollowLeash => followLeash;
    public float PoundRange => poundRange;
    public bool IsActive => IsAlive(alpha);

    [Header("격노 관련 값")]
    [SF] private float cooldownReduce = 0.25f; 
    [SF] private float minCooldownRate = 0.4f;

    private readonly List<WolfMove> members = new();
    private readonly List<WolfMove> alive = new();
    private float phaseTimer;
    private float turnAngle;
    private int startNormalWolfCount;
    private WolfCombat alphaCombat;
    private bool engaged;

    private void Awake()
    {
        members.Add(alpha);
        members.AddRange(normals);

        foreach (WolfMove mover in members)
            mover.JoinPack(this);
        alphaCombat = alpha.GetComponent<WolfCombat>();
        startNormalWolfCount = members.Count;
    }

    private void Update()
    {
        // 포톤 사용하면 if (!PhotonNetwork.IsMasterClient) return;
        RefreshAlive();

        // 알파 사망 시 로직 해제, 노말은 기본 MonsterMove 로직으로 동작
        if (!IsActive)
        {
            SetEngaged(false);
            enabled = false;
            return;
        }

        RefreshTarget();
        SetEngaged(Target != null);

        if (Target == null && Phase != PackPhase.Patrol)
            ChangePhase(PackPhase.Patrol);

        phaseTimer += Time.deltaTime;
        turnAngle += turnSpeed * Time.deltaTime;

        switch (Phase)
        {
            case PackPhase.Patrol:
                if (Target != null) 
                    ChangePhase(PackPhase.Encircle);
                break;

            case PackPhase.Encircle:
                if (phaseTimer >= GetRageCooldownReduce(poundDelay)) 
                    ChangePhase(PackPhase.Pound);
                break;

            case PackPhase.Pound:
                if (phaseTimer >= poundTimeout) 
                    ChangePhase(PackPhase.Assault);
                break;

            case PackPhase.Assault:
                if (phaseTimer >= GetRageCooldownReduce(poundCooldown)) 
                    ChangePhase(PackPhase.Encircle);
                break;
        }
    }
    

    public bool CanAcquire(GameObject target) => reachability.CanAcquire(target);
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

        if (next == PackPhase.Assault)
        {
            foreach (WolfMove m in alive)
                m.attackTimer = m.attackSpeed - Random.Range(0f, 0.5f);
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
    /// <summary>
    /// 생존한 늑대 갱신
    /// </summary>
    private void RefreshAlive()
    {
        alive.Clear();
        foreach (WolfMove m in members)
            if (IsAlive(m)) alive.Add(m);
    }

    /// <summary>
    /// 타겟 해제/재설정 로직
    /// </summary>
    private void RefreshTarget()
    {
        // 거리 이탈 or 도달 불가 지속 → 해제
        if (Target != null && (!IsTargetValid(Target) ||
            reachability.ShouldDrop(alpha.transform.position, Target, Time.deltaTime)))
        {
            Target = null;
            foreach (WolfMove m in alive)
                m.nowTarget = null;
        }

        // 쿨다운 중인 타겟은 제외
        if (Target == null)
        {
            foreach (WolfMove m in alive)
            {
                GameObject t = m.nowTarget;
                if (t != null && t.activeInHierarchy && reachability.CanAcquire(t))
                {
                    Target = t;
                    break;
                }
            }
        }

        foreach (WolfMove m in alive)
            m.nowTarget = Target;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    private bool IsTargetValid(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;

        float sqr = loseDistance * loseDistance;
        foreach (WolfMove m in alive)
            if ((m.transform.position - target.transform.position).sqrMagnitude <= sqr)
                return true;

        return false;
    }

    /// <summary>
    /// 늑대의 생존 여부 판별 함수
    /// </summary>
    /// <param name="m"></param>
    /// <returns>생존 : true</returns>
    private static bool IsAlive(WolfMove m)
    {
        if (m == null || !m.isActiveAndEnabled) return false;
        return m.state == null || m.state.GetStateType() != MonsterState.Death;
    }

    /// <summary>
    /// 이동 가능한 Nav 매쉬 지역 탐색
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private static Vector3 SampleNav(Vector3 point)
    {
        return NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position
            : point;
    }

    /// <summary>
    /// 땅찍기 공격의 쿨다운 계산 로직
    /// 일반 늑대 사망 시 마다 쿨다운이 짧아짐
    /// </summary>
    /// <returns>실질 쿨다운 float</returns>
    private float GetRageCooldownReduce(float cooldown)
    {
        int dead = startNormalWolfCount - alive.Count;
        float rate = Mathf.Max(minCooldownRate, 1f - dead * cooldownReduce);
        return cooldown * rate;
    }

    /// <summary>
    /// 늑대 상태창 및 연결 판단 이벤트
    /// </summary>
    /// <param name="value"></param>
    private void SetEngaged(bool value)
    {
        if (engaged == value) return;

        NormalMonster data = alphaCombat.GetData();
        if (data == null) return;

        engaged = value;
        if (value) BossEvents.Engage(data, data.Name);
        else BossEvents.Disengage(data);
    }
    #endregion

    private void OnDisable()
    {
        SetEngaged(false);
    }

    private void OnDrawGizmosSelected()
    {
        if (Target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Target.transform.position, turnRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(Target.transform.position, poundRange);
    }
}
