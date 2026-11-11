using System;

public static class JJH_Enums
{
    public enum MonsterState { 
        Spawn, Idle, Patrol, Chase, 
        // 늑대 전용
        Follow, Encircle, Pound, 
        //
        Wait, Attack, Death, None }
}
