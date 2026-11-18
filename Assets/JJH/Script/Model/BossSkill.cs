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
}
