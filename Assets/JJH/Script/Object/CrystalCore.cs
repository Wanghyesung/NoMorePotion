using Enzeun.Runtime;
using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

using static CommonStruct;

using SF = UnityEngine.SerializeField;

/// <summary>
/// 지켜야 하는 방어 오브젝트 (수정 구슬)
/// 몬스터의 공격 대상(AttackTarget, IsObjective)이자 피격 대상(IDamageable)
/// </summary>
[RequireComponent(typeof(AttackTarget))]
public class CrystalCore : MonoBehaviour, IDamageable, IHealable
{
    [Header("체력")]
    [SF] private int maxHp = 5000;
    //임시
    [SF] private string displayName = "수정 구슬";

    [Header("UI (선택)")]
    [SF] private MonsterHPHud hud;   // 화면 상단 오버레이 등 (hideIsFull, faceCamera 끄기)

    private AttackTarget attackTarget;
    private int hp;

    public int HP => hp;
    public int MaxHP => maxHp;
    public bool IsBroken => hp <= 0;

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
        hp = maxHp;
        attackTarget.SetTargetable(true);

        if (hud != null)
            hud.Init(maxHp, hp, displayName);
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

    // 플레이어(아군) 공격은 무시
    public void TakeDamage(AttackInfo attackInfo, Character attacker) { }

    public void Heal(int amount)
    {
        if (IsBroken || amount <= 0) return;
        ChangeHP(Mathf.Min(maxHp, hp + amount));
    }
    #endregion

    /// <summary>
    /// 수정 구슬을 일정한 대미지만 받음(모든 저항 0)
    /// </summary>
    /// <param name="damage"></param>
    private void ApplyDamage(int damage)
    {
        if (!GameAuthority.HasAuthority) return;   // 체력 판정은 권한 쪽에서만
        if (IsBroken || damage <= 0) return;

        ChangeHP(Mathf.Max(0, hp - damage));
        Debug.Log(hp);
        if (IsBroken)
            Break();
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
        OnBroken?.Invoke(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnBroken = null;
}
;