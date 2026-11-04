using Newtonsoft.Json;
using UnityEngine;

public class BossSkill
{
    public int UseMonsterId;
    public int SkillNum;
    public string AnimationName;
    public int SkillDamage;

    [JsonIgnore]
    public float Cooldown;
}
