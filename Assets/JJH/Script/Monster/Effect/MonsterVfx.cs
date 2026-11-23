using System;
using UnityEngine;
using MoreMountains.Feedbacks;

using SF = UnityEngine.SerializeField;

/// <summary>
/// 경고 표시를 어디에 띄울지
/// </summary>
public enum WarningPlace
{
    Self,    // 보스 발밑
    Front,   // 보스 앞쪽
    Target,  // 타겟 위치
    Random   // 범위 내 랜덤

}

/// <summary>
/// 공격 하나(기본 공격 또는 스킬 칸)의 시각 효과 설정
/// </summary>
[Serializable]
public class AttackVfx
{
    [Header("타격 파티클(선택)")]
    public GameObject HitEffect;       
    // 생성 위치 (입, 발 등), 비우면 몬스터 루트
    public Transform HitPoint;

    [Header("경고 표시 (스킬만)")]
    // 비우면 경고 x
    public GameObject Warning;
    public WarningPlace Place = WarningPlace.Front;
    // 경고 scale
    public float WarningSize = 3f;
    // WarningPlace.Front 전용
    public float FrontOffset = 2f;

    // 비워도 됨
    [Header("충격 흔들림")]
    public MMF_Player Impact;
}

public interface IWarningIndicator
{
    void Play(float duration);
}

/// <summary>
/// 몬스터 공격 시각 효과 (파티클 + 경고 표시)
/// 칸 번호: 0 = 기본 공격, 1~4 = 스킬 칸
/// </summary>
public class MonsterVfx : MonoBehaviour
{
    [SF] private AttackVfx normalAttack = new();
    [SF] private AttackVfx[] skills = new AttackVfx[4];

    private MonsterMove mover;
    private readonly GameObject[] activeWarnings = new GameObject[5];
    private readonly Vector3[] lastWarningPos = new Vector3[5];
    private readonly bool[] hasWarningPos = new bool[5];

    private void Awake()
    {
        mover = GetComponent<MonsterMove>();
    }




    /// <summary>
    /// 타겟이 지정된 최종 위치
    /// </summary>
    /// <param name="slot">지정한 스킬</param>
    /// <returns>지정된 위치 (Vector3)</returns>
    public Vector3 GetLastWarningPosition(int slot)
    {
        if (hasWarningPos[slot]) return lastWarningPos[slot];

        AttackVfx vfx = GetSlot(slot);
        return vfx != null ? GetWarningPosition(vfx) : transform.position;
    }

    public void ResetWarningPositions() => Array.Clear(hasWarningPos, 0, hasWarningPos.Length);

    /// <summary>
    /// 지정된 슬롯에 배정된 스킬 이펙트 획득
    /// </summary>
    /// <param name="slot"></param>
    /// <returns></returns>
    private AttackVfx GetSlot(int slot)
    {
        if (slot == 0) return normalAttack;
        int idx = slot - 1;
        return idx >= 0 && idx < skills.Length ? skills[idx] : null;
    }

    #region 타격 파티클
    /// <summary>
    /// 타격 파티클 표시용 함수
    /// </summary>
    /// <param name="slot"></param>
    public void PlayHit(int slot)
    {
        AttackVfx vfx = GetSlot(slot);
        if (vfx == null || vfx.HitEffect == null) return;

        Vector3 pos;
        Quaternion rot;

        // 타겟인 경우
        if (vfx.Place == WarningPlace.Target && vfx.Warning != null)
        {
            pos = GetLastWarningPosition(slot);
            rot = Quaternion.identity;
        }
        // 지정형인 경우
        else
        {
            Transform point = vfx.HitPoint != null ? vfx.HitPoint : transform;
            pos = point.position;
            rot = point.rotation;
        }

        PoolManager.Instance.Get(vfx.HitEffect, pos, rot);
        PlayImpact(slot, pos);
    }
    #endregion

    #region 경고 표시
    /// <summary>
    /// 경고 표시용 함수
    /// </summary>
    /// <param name="slot">지정 슬롯 Num</param>
    /// <param name="duration">경고 유지 시간 (애니메이션 이벤트의 float 값, 0이면 채우기 연출 없음)</param>

    /// <summary>
    /// 단순 스킬 지정형 경고 표시
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="duration"></param>
    public void ShowWarning(int slot, float duration)
    {
        AttackVfx vfx = GetSlot(slot);
        if (vfx == null || vfx.Warning == null) return;

        ShowWarningAt(slot, GetWarningPosition(vfx), duration);
    }

    /// <summary>
    /// 위치를 직접 지정해 경고 표시
    /// </summary>
    public void ShowWarningAt(int slot, Vector3 pos, float duration)
    {
        AttackVfx vfx = GetSlot(slot);
        if (vfx == null || vfx.Warning == null) return;

        HideWarning(slot);

        lastWarningPos[slot] = pos;
        hasWarningPos[slot] = true;

        Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        GameObject warning = PoolManager.Instance.Get(vfx.Warning, pos, rot);
        warning.transform.localScale = Vector3.one * vfx.WarningSize;

        if (duration > 0f && warning.TryGetComponent(out IWarningIndicator indicator))
            indicator.Play(duration);

        activeWarnings[slot] = warning;
    }

    public void HideWarning(int slot)
    {
        if (slot < 0 || slot >= activeWarnings.Length) return;
        if (activeWarnings[slot] == null) return;

        PoolManager.Instance.Release(activeWarnings[slot]);
        activeWarnings[slot] = null;
    }

    /// <summary>
    /// 사망, 반납, 모션 끊김 등으로 남은 경고 전부 정리
    /// </summary>
    public void HideAllWarnings()
    {
        for (int i = 0; i < activeWarnings.Length; i++)
            HideWarning(i);
    }

    /// <summary>
    /// VFX내 벡터 값 추출 함수
    /// </summary>
    /// <param name="vfx">지정된 VFX 객체</param>
    /// <returns>지정 위치 값 (Vector3)</returns>
    private Vector3 GetWarningPosition(AttackVfx vfx)
    {
        switch (vfx.Place)
        {
            case WarningPlace.Target when mover != null && mover.nowTarget != null:
                return mover.nowTarget.transform.position;

            case WarningPlace.Target:
            case WarningPlace.Front:
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return transform.position + forward.normalized * vfx.FrontOffset;

            default:
                return transform.position;
        }
    }
    #endregion

    /// <summary>
    /// 칸의 충격 연출을 지정 위치에서 재생
    /// </summary>
    public void PlayImpact(int slot, Vector3 pos)
    {
        AttackVfx vfx = GetSlot(slot);
        if (vfx == null || vfx.Impact == null) return;

        vfx.Impact.PlayFeedbacks(pos);
    }

    private void OnDisable()
    {
        if (PoolManager.Instance != null)
            HideAllWarnings();
    }
}
