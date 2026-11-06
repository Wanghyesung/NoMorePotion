using Newtonsoft.Json;

public class BossSkill
{
    public int UseMonsterId;
    public int SkillNum;
    public string AnimationName;
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
