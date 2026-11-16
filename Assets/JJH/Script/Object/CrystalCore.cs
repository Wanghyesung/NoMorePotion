using Enzeun.Runtime;
using Photon.Pun;
using System;
using UnityEngine;

using static CommonStruct;

using SF = UnityEngine.SerializeField;

/// <summary>
/// 지켜야 하는 방어 오브젝트 (수정 구슬)
/// 몬스터의 공격 대상(AttackTarget, IsObjective)이자 피격 대상(IDamageable)
/// </summary>
[RequireComponent(typeof(AttackTarget))]
[RequireComponent(typeof(PhotonView))]
public class CrystalCore : MonoBehaviourPunCallbacks, IDamageable, IHealable
{
    [Header("체력")]
    [SF] private int maxHp = 5000;
    //임시
    [SF] private string displayName = "수정 구슬";

    [Header("조각 파괴 관련")]
    [SF] private Renderer body;               // 원본 구슬 렌더러
    [SF] private Collider bodyCollider;       // 조각과 겹치지 않게 파괴 시 끔
    [SF] private GameObject fracturedRoot;    // 조각 모델 (평소 꺼둠)

    [Header("UI (선택)")]
    [SF] private MonsterHPHud hud;   // 화면 상단 오버레이 등 (hideIsFull, faceCamera 끄기)
    private bool brokenApplied;   // 파괴 연출이 이미 적용됐는지 (중복 방지)

    private AttackTarget attackTarget;
    private int hp;

    public int HP => hp;
    public int MaxHP => maxHp;
    public bool IsBroken => hp <= 0;

    private bool IsNetworked => PhotonNetwork.InRoom;

    /// <summary>
    /// 체력 변화 (변경 전, 변경 후)
    /// </summary>
    public event Action<int, int> OnHPChanged;

    /// <summary>
    /// 파괴 알림 (게임 매니저 구독용)
    /// </summary>
    public static event Action<CrystalCore> OnBroken;

    private void Awake()
    {
        attackTarget = GetComponent<AttackTarget>();
    }

    private void Start()
    {
        ResetHP();
    }

    /// <summary>
    /// 체력 초기화
    /// </summary>
    public void ResetHP()
    {
        ApplyReset();

        if (IsNetworked && PhotonNetwork.IsMasterClient)
            photonView.RPC(nameof(RpcReset), RpcTarget.Others);
    }

    #region 인터페이스
    public void TakeDamage(AttackInfo attackInfo)
    {
        ApplyDamage(attackInfo.Damage);
    }

    public void TakeDamage(AttackInfo attackInfo, IAttacker attacker)
    {
        ApplyDamage(attackInfo.Damage);
    }

    public void TakeDamage(AttackInfo attackInfo, Character attacker) { }

    /// <summary>
    /// 회복은 누가 시전하든 방장에게 넘겨서 처리
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;

        if (IsNetworked && !PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(nameof(RpcHeal), RpcTarget.MasterClient, amount);
            return;
        }
        RpcHeal(amount);
    }
    #endregion

    /// <summary>
    /// 수정 구슬은 일정한 대미지만 받음(모든 저항 0)
    /// </summary>
    private void ApplyDamage(int damage)
    {
        if (!GameAuthority.HasAuthority) return;
        if (IsBroken || damage <= 0) return;

        SetHPAuthority(Mathf.Max(0, hp - damage));
    }

    private void ChangeHP(int newHp)
    {
        int before = hp;
        hp = newHp;

        OnHPChanged?.Invoke(before, hp);
        if (hud != null) hud.HealthChange(before, hp);
    }

    private void Break()
    {
        attackTarget.SetTargetable(false);
        SwapToFractured();
        OnBroken?.Invoke(this);
    }

    private void SwapToFractured()
    {
        if (body != null) body.enabled = false;
        if (bodyCollider != null) bodyCollider.enabled = false;
        if (fracturedRoot != null) fracturedRoot.SetActive(true);
    }

    private void SetHPAuthority(int newHp)
    {
        if (newHp == hp) return;

        ApplyHP(newHp);

        if (IsNetworked)
            photonView.RPC(nameof(RpcSyncHP), RpcTarget.Others, hp);
    }

    private void ApplyReset()
    {
        hp = maxHp;
        brokenApplied = false;
        attackTarget.SetTargetable(true);
        RestoreVisual();

        if (hud != null)
            hud.Init(maxHp, hp, displayName);
    }

    /// <summary>
    /// 체력 반영 + 연출 (모든 클라이언트 공통)
    /// </summary>
    private void ApplyHP(int newHp)
    {
        int before = hp;
        hp = newHp;

        OnHPChanged?.Invoke(before, hp);
        if (hud != null) hud.HealthChange(before, hp);

        if (IsBroken)
            ApplyBreak();
    }

    private void RestoreVisual()
    {
        if (body != null) body.enabled = true;
        if (bodyCollider != null) bodyCollider.enabled = true;
        if (fracturedRoot != null) fracturedRoot.SetActive(false);
    }

    private void ApplyBreak()
    {
        if (brokenApplied) return;
        brokenApplied = true;

        attackTarget.SetTargetable(false);
        SwapToFractured();
        OnBroken?.Invoke(this);
    }

    #region RPC
    [PunRPC]
    private void RpcReset() => ApplyReset();



    [PunRPC]
    private void RpcHeal(int amount)
    {
        if (IsBroken) return;
        SetHPAuthority(Mathf.Min(maxHp, hp + amount));
    }

    [PunRPC]
    private void RpcSyncHP(int newHp) => ApplyHP(newHp);
    #endregion

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnBroken = null;
}
;