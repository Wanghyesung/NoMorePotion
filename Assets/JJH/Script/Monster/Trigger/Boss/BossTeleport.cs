using Cysharp.Threading.Tasks;
using Photon.Pun;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using SF = UnityEngine.SerializeField;

public class BossTeleport : MonoBehaviourPun
{
    [SF] private MonsterMove mover;
    [SF] private MonsterCombat combat;
    [SF] private MonsterVfx vfx;
    [SF] private PhotonTransformView transformView;

    [Header("도착 위치")]
    [SF] private float arriveDistance = 1.5f;
    //도착 후보 지점에서 NavMesh를 찾을 반경
    [SF] private float sampleRadius = 2f;

    [Header("연출")]
    //도착 경고를 띄울 MonsterVfx 칸 (스킬 번호)
    [SF] private int warningSlot = 2;
    [SF] private Transform modelRoot;
    [SF] private GameObject vanishEffect;
    [SF] private GameObject appearEffect;
    // TeleportBegin(Float 경고 시간) → TeleportVanish → TeleportArrive
    // 정면이 막히면 각도를 바꿔 가며 시도

    [Header("도착 공격")]
    [Tooltip("도착 순간 잠깐 켤 히트박스 (SkillAttackTrigger, skillNum = 스킬 번호). 없으면 공격 없음")]
    [SF] private GameObject arriveHitbox;
    [SF] private float arriveHitTime = 0.15f;

    private static readonly float[] TryAngles = { 0f, 45f, -45f, 90f, -90f, 180f };

    private Renderer[] modelRenderers;
    private Vector3 destination;
    private bool hasDestination;

    private void Awake()
    {
        if (mover == null) mover = GetComponent<MonsterMove>();
        if (combat == null) combat = GetComponent<MonsterCombat>();
        if (vfx == null) vfx = GetComponent<MonsterVfx>();
        if (transformView == null) transformView = GetComponent<PhotonTransformView>();

        modelRenderers = (modelRoot != null ? modelRoot : transform)
            .GetComponentsInChildren<Renderer>(true)
            .Where(r => r is SkinnedMeshRenderer || r is MeshRenderer)
            .ToArray();
    }

    private void OnEnable()
    {
        if (combat != null) combat.Died += OnDied;
    }

    private void OnDisable()
    {
        if (combat != null) combat.Died -= OnDied;
        ResetTeleport();
    }

    // 숨어 있는 도중에 죽어도 사망 연출은 보이게
    private void OnDied(MonsterCombat _) => ResetTeleport();

    private void ResetTeleport()
    {
        hasDestination = false;
        SetVisible(true);
        if (vfx != null) vfx.HideWarning(warningSlot);
        if (arriveHitbox != null) arriveHitbox.SetActive(false);
    }

    #region 애니메이션 이벤트 (방장만 처리)
    public void Begin(float warningTime)
    {
        if (!GameAuthority.HasAuthority) return;
        Send(nameof(RpcBegin), FindDestination(), warningTime);
    }

    public void Vanish()
    {
        if (!GameAuthority.HasAuthority) return;
        Send(nameof(RpcVanish));
    }

    public void Arrive()
    {
        if (!GameAuthority.HasAuthority) return;
        Send(nameof(RpcArrive));
    }

    private void Send(string method, params object[] args)
    {
        if (PhotonNetwork.InRoom)
            photonView.RPC(method, RpcTarget.All, args);
        else
            SendMessage(method, args.Length == 0 ? null : args, SendMessageOptions.DontRequireReceiver);
    }
    #endregion

    #region RPC (모든 클라이언트)
    [PunRPC]
    private void RpcBegin(Vector3 dest, float warningTime)
    {
        destination = dest;
        hasDestination = true;
        if (vfx != null) vfx.ShowWarningAt(warningSlot, dest, warningTime);
    }

    [PunRPC]
    private void RpcVanish()
    {
        SpawnEffect(vanishEffect, transform.position);
        SetVisible(false);
    }

    [PunRPC]
    private void RpcArrive()
    {
        if (hasDestination) MoveTo(destination);
        hasDestination = false;

        if (vfx != null) vfx.HideWarning(warningSlot);
        SpawnEffect(appearEffect, transform.position);
        if (vfx != null) vfx.PlayImpact(warningSlot, transform.position);
        SetVisible(true);
        FaceTarget();

        if (arriveHitbox != null)
            ArriveHit().Forget();
    }
    #endregion

    /// <summary>
    /// 대상과 보스 사이, 대상에게서 arriveDistance 떨어진 NavMesh 위 지점
    /// </summary>
    private Vector3 FindDestination()
    {
        GameObject target = mover.nowTarget;
        if (target == null) return transform.position;

        int area = mover.agent.areaMask;
        Vector3 targetPos = target.transform.position;
        bool targetOnMesh = NavMesh.SamplePosition(targetPos, out NavMeshHit targetHit, sampleRadius, area);

        Vector3 dir = transform.position - targetPos;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = -target.transform.forward;
        dir.Normalize();

        foreach (float angle in TryAngles)
        {
            Vector3 candidate = targetPos + Quaternion.Euler(0f, angle, 0f) * dir * arriveDistance;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius, area)) continue;

            // 대상과 도착점 사이에 벽이 있으면 제외 (벽 너머 도착 방지)
            if (targetOnMesh && NavMesh.Raycast(targetHit.position, hit.position, out _, area)) continue;

            return hit.position;
        }

        return transform.position;   // 자리가 없으면 제자리 재등장
    }

    /// <summary>
    /// 도착 지점에서 히트박스를 잠깐 켬 (위치 이동과 같은 순간이라 모든 클라이언트에서 같은 위치)
    /// </summary>
    private async UniTaskVoid ArriveHit()
    {
        arriveHitbox.SetActive(true);

        await UniTask.Delay(TimeSpan.FromSeconds(arriveHitTime),
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();

        if (arriveHitbox != null)
            arriveHitbox.SetActive(false);
    }

    private void MoveTo(Vector3 pos)
    {
        if (GameAuthority.HasAuthority && mover.agent.enabled)
        {
            mover.agent.Warp(pos);
            mover.agent.ResetPath();
            return;
        }

        // 비권한: 보간으로 맵을 가로질러 미끄러지지 않게 바로 옮기고,
        // 뷰를 다시 켜서 다음 수신 위치에 스냅되게 함
        if (transformView != null) transformView.enabled = false;
        transform.position = pos;
        if (transformView != null) transformView.enabled = true;
    }

    private void FaceTarget()
    {
        GameObject target = mover.nowTarget;
        if (target == null) return;

        Vector3 look = target.transform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    private void SetVisible(bool visible)
    {
        if (modelRenderers == null) return;
        foreach (Renderer r in modelRenderers)
            if (r != null) r.enabled = visible;
    }

    private static void SpawnEffect(GameObject prefab, Vector3 pos)
    {
        if (prefab != null)
            PoolManager.Instance.Get(prefab, pos, Quaternion.identity);
    }
}
