using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;

using SF = UnityEngine.SerializeField;

public class WolfCombat : MonsterCombat
{
    [SF] private Collider poundHitbox;

    [Header("도약 준비 시간")]
    [SF] private float crouchClipTime = 0.17f;  // 클립상 '웅크림 시작 → 이륙' 구간 길이
    [SF] private float windupDuration = 0.2f;   // 실제로 늘릴 준비 시간
    [SF] private float windupTurnSpeed = 8f;

    [Header("Pound 점프")]
    [SF] private float jumpHeight = 2.5f;
    [SF] private float jumpDuration = 0.45f;
    [SF] private float landingLeadTime = 0.33f; // 클립상 '이륙 이벤트 → 임팩트 프레임' 시간
    [SF] private float landOffset = 0.3f;
    [SF] private float maxJumpDistance = 8f;

    private WolfAnamationController WolfAnim => anim as WolfAnamationController;
    private WolfMove WolfMover => mover as WolfMove;

    private CancellationTokenSource jumpCts;
    private bool isJumping;
    private bool isWindup;

    private WolfBoss BossData => data as WolfBoss;

    private void Update()
    {
        if (isWindup)
            FaceTargetDuringWindup();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        // 도약 준비 중 비활성화 시 재생 속도 복구
        isWindup = false;
        WolfAnim.ResetPlayback();

        // 체공 중 비활성화 시 버그 방지
        if (!isJumping) return;
        jumpCts?.Cancel();
        EndJump(transform.position);
    }


    public void Init(string name,int maxHp, int hp, Dictionary<DamageType, int> immune, float speed,
        int id, int instanceId, string spawnArea, string description,
        List<BossSkill> bossSkill, DamageType damageType)
    {
        SetData(new WolfBoss(name, maxHp, hp, immune, speed, id, instanceId,
            spawnArea, description, bossSkill, damageType));
    }


    #region 땅찍기 애니메이션 이벤트
    /// <summary>
    /// 점프 공격 애니메이션 시작
    /// </summary>
    private void PoundAttackAnimationStart()
    {
        mover.IsAnimation = true;
    }

    /// <summary>
    /// 도약 준비(웅크림) 시작
    /// </summary>
    private void PoundWindup()
    {
        isWindup = true;
        float scale = crouchClipTime / Mathf.Max(0.01f, windupDuration);
        WolfAnim.SetPlaybackScale(Mathf.Max(0.05f, scale));
        // 경고 이펙트/사운드는 여기서 재생
    }

    /// <summary>
    /// 점프 직후
    /// </summary>
    private void PoundTakeoff()
    {
        isWindup = false;

        if (isJumping || mover.nowTarget == null)
        {
            WolfAnim.ResetPlayback();
            return;
        }

        jumpCts = new CancellationTokenSource();
        PoundJump(jumpCts.Token).Forget();
    }

    /// <summary>
    /// 점프 공격 판정 ON
    /// </summary>
    private void PoundAttackOn()
    {
        poundHitbox.gameObject.SetActive(true);
    }

    /// <summary>
    /// 점프 공격 판정 OFF
    /// </summary>
    private void PoundAttackOff()
    {
        poundHitbox.gameObject.SetActive(false);
    }

    /// <summary>
    /// 점프 공격 애니메이션 종료
    /// </summary>
    private void PoundAttackAnimationEnd()
    {
        poundHitbox.gameObject.SetActive(false);
        isWindup = false;
        WolfAnim.ResetPlayback();
        mover.IsAnimation = false;
        WolfAnim.IsPound(false);

        if (WolfMover != null && WolfMover.Pack != null)
            WolfMover.Pack.OnPoundFinished();
    }
    #endregion

    #region 도약 준비 이벤트
    private void FaceTargetDuringWindup()
    {
        if (mover.nowTarget == null) return;

        Vector3 dir = mover.nowTarget.transform.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir), Time.deltaTime * windupTurnSpeed);
    }
    #endregion

    #region 점프 준비 동작 유니태스크
    private async UniTaskVoid PoundJump(CancellationToken token)
    {
        isJumping = true;

        NavMeshAgent agent = mover.agent;
        Vector3 start = transform.position;
        Vector3 end = GetLandingPoint(start);

        Vector3 dir = end - start;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);

        agent.ResetPath();
        agent.updatePosition = false;

        WolfAnim.SetPlaybackScale(0f);

        float time = 0f;
        bool resumed = false;

        while (time < jumpDuration)
        {
            time += Time.deltaTime;
            float n = Mathf.Clamp01(time / jumpDuration);

            Vector3 pos = Vector3.Lerp(start, end, n);
            pos.y += 4f * jumpHeight * n * (1f - n);
            transform.position = pos;

            if (!resumed && jumpDuration - time <= landingLeadTime)
            {
                WolfAnim.ResetPlayback();
                resumed = true;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        EndJump(end);
    }

    private Vector3 GetLandingPoint(Vector3 start)
    {
        Vector3 flat = mover.nowTarget.transform.position - start;
        flat.y = 0;

        float dist = Mathf.Clamp(flat.magnitude - landOffset, 0f, maxJumpDistance);
        Vector3 point = start + flat.normalized * dist;

        // 벽 너머로 뛸 경우 벽 부딪침 방지
        if (NavMesh.Raycast(start, point, out NavMeshHit wallHit, NavMesh.AllAreas))
            point = wallHit.position;

        // 길이 없는 곳으로 뛸 경우, 추락 방지
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            point = hit.position;

        return point;
    }

    private void EndJump(Vector3 landPos)
    {
        isJumping = false;
        jumpCts?.Dispose();
        jumpCts = null;

        WolfAnim.ResetPlayback();

        NavMeshAgent agent = mover.agent;
        if (agent.isActiveAndEnabled)
            agent.Warp(landPos);
        agent.updatePosition = true;
    }
    #endregion

    public BossSkill GetBossSkill(string animationName)
    {
        return BossData.BossSkill.Find(x => x.AnimationName == animationName);
    }
}
