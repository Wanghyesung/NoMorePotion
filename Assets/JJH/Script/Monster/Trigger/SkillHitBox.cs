using System;
using UnityEngine;

[Serializable]
public class SkillHitbox
{
    public string Name;
    public Collider Hitbox;
    [Tooltip("0 이상이면 켤 때 그 칸의 경고 위치로 이동 (번개 떨구기 같은 타겟 지정 스킬)")]
    public int FollowWarningSlot = -1;
}