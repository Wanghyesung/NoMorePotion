using Enzeun.SO;
using Newtonsoft.Json;

public class BossSkill
{
    public int UseMonsterId;
    /// <summary>
    /// 칸 번호 1~4 (Animator Bool Skill1~4, MonsterVfx 칸, 히트박스 공용)
    /// </summary>
    public int SkillNum;
    /// <summary>
    /// 기획 확인용 이름 (로직에서 사용 안 함)
    /// </summary>
    public string Name;
    /// <summary>
    /// 공격력 비례 배율
    /// </summary>
    public int SkillDamage;
    public float MinRange;
    public float MaxRange;
    public float Cooldown;
    public float Weight = 1f;
    public bool Scripted;

    /// <summary>
    /// 적중 시 거는 상태 효과 ID
    /// <para>0 = 없음</para>
    /// </summary>
    public int StatusEffectId;
    public int StatusPercent;
    public float StatusDuration;

    [JsonIgnore]
    public StatusEffectData StatusEffect;   // 로드 시 ID로 찾아 둠
}
