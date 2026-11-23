using Cysharp.Threading.Tasks;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;
using static CommonStruct;
using static JJH_Constants;
using Random = UnityEngine.Random;
using SF = UnityEngine.SerializeField;

public class MeteorRain : MonoBehaviourPun
{
    [SF] private MonsterCombat combat;
    [SF] private MonsterMove mover;
    [Tooltip("데미지 배율을 가져올 스킬 번호")]
    [SF] private int skillNum = 4;

    [Header("배치")]
    [SF] private int count = 8;
    [Tooltip("타겟 범위")]
    [SF] private float radius = 10f;

    [Header("타이밍")]
    [Tooltip("경고가 뜬 뒤 착탄까지 시간")]
    [SF] private float warningTime = 1.2f;
    [Tooltip("메테오 사이 간격")]
    [SF] private float interval = 0.2f;

    [Header("판정")]
    [SF] private float hitRadius = 2f;

    [Header("연출")]
    [SF] private GameObject warningPrefab;
    [SF] private float warningSize = 2f;
    // 메테오 프리팹
    [SF] private GameObject fallEffect;
    // 착탄 이펙트
    [SF] private GameObject impactEffect;

    private readonly Collider[] hitBuffer = new Collider[16];
    private readonly HashSet<IDamageable> hitSet = new();
    private readonly List<GameObject> activeWarnings = new();
    private CancellationTokenSource cts;

    private void Awake()
    {
        if (combat == null) 
            combat = GetComponent<MonsterCombat>();

        if (mover == null) 
            mover = GetComponent<MonsterMove>();
    }

    private void OnDisable() => Cancel();

    #region 시전
    public void Cast()
    {
        if (!GameAuthority.HasAuthority) return;

        GameObject target = mover.nowTarget;
        Vector3 center = target != null ? target.transform.position : transform.position;
        int seed = Random.Range(int.MinValue, int.MaxValue);

        if (PhotonNetwork.InRoom)
            photonView.RPC(nameof(RpcMeteor), RpcTarget.All, seed, center);
        else
            RpcMeteor(seed, center);
    }

    [PunRPC]
    private void RpcMeteor(int seed, Vector3 center)
    {
        Cancel();
        cts = new CancellationTokenSource();
        Rain(PickPoints(seed, center), cts.Token).Forget();
    }
    #endregion

    
    /// <summary>
    /// 메테오 착탄 위치 지정 함수
    /// </summary>
    /// <param name="seed">랜덤 시드(동일할 경우 동일한 랜덤값)</param>
    /// <param name="center">첫 착탄 중앙 Vector값</param>
    /// <returns></returns>
    private Vector3[] PickPoints(int seed, Vector3 center)
    {
        var rng = new System.Random(seed);
        var points = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Vector3 p = center;
            if (i > 0)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                // 원 안에 고르게
                float dist = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                p += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
            }

            // 바닥 높이에 맞춤 (NavMesh 밖이면 원래 위치 유지)
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                p = hit.position;

            points[i] = p;
        }
        return points;
    }

    /// <summary>
    /// 메테오 착탄 반복 함수
    /// </summary>
    /// <param name="points"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async UniTaskVoid Rain(Vector3[] points, CancellationToken token)
    {
        foreach (Vector3 p in points)
        {
            Drop(p, token).Forget();
            if (await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token)
                .SuppressCancellationThrow())
                return;
        }
    }

    /// <summary>
    /// 메테오 착륙 이펙트 함수
    /// </summary>
    /// <param name="point"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async UniTaskVoid Drop(Vector3 point, CancellationToken token)
    {
        GameObject warning = null;
        if (warningPrefab != null)
        {
            warning = PoolManager.Instance.Get(warningPrefab, point, Quaternion.identity);
            warning.transform.localScale = Vector3.one * warningSize;
            if (warning.TryGetComponent(out IWarningIndicator indicator))
                indicator.Play(warningTime);
            activeWarnings.Add(warning);
        }

        if (fallEffect != null)
            PoolManager.Instance.Get(fallEffect, point, Quaternion.identity);

        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(warningTime),
            cancellationToken: token).SuppressCancellationThrow();

        if (warning != null && activeWarnings.Remove(warning))
            PoolManager.Instance.Release(warning);
        if (canceled) return;

        if (impactEffect != null)
            PoolManager.Instance.Get(impactEffect, point, Quaternion.identity);

        HitAt(point);
    }

    /// <summary>
    /// 착탄 지점 판정
    /// </summary>
    private void HitAt(Vector3 point)
    {
        int n = Physics.OverlapSphereNonAlloc(point, hitRadius, hitBuffer, ~0, QueryTriggerInteraction.Collide);
        if (n == 0) return;

        AttackInfo info = combat.CreateSkillAttackInfo(skillNum);
        IAttacker attacker = combat.GetData();
        hitSet.Clear();

        for (int i = 0; i < n; i++)
        {
            Collider other = hitBuffer[i];
            if (!(other.CompareTag(PLAYER_TAG) || other.CompareTag(CORE_TAG))) continue;
            if (!AttackTarget.IsValid(other.gameObject)) continue;
            if (!MonsterAttackTrigger.IsHitTrigger(other.gameObject)) continue;
            if (!other.TryGetComponent(out IDamageable target)) continue;
            if (!hitSet.Add(target)) continue;

            target.TakeDamage(info, attacker);
        }
    }

    /// <summary>
    /// 사망·반납 시 남은 메테오 취소 + 경고 정리
    /// </summary>
    private void Cancel()
    {
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        foreach (GameObject w in activeWarnings)
            if (w != null) PoolManager.Instance.Release(w);
        activeWarnings.Clear();
    }
}
