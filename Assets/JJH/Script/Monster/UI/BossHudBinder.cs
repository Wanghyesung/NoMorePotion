using Firebase.Auth;
using System.Runtime.InteropServices;
using UnityEngine;
using SF = UnityEngine.SerializeField;

public class BossHudBinder : MonoBehaviour
{
    [SF] private MonsterHPHud hud;
    private NormalMonster bound;

    private void OnEnable()
    {
        BossEvents.OnEngaged += Bind;
        BossEvents.OnDisengaged += Unbind;
    }

    private void OnDisable()
    {
        BossEvents.OnEngaged -= Bind;
        BossEvents.OnDisengaged -= Unbind;
        Unbind(bound);
    }

    private void Bind(NormalMonster data, string bossName)
    {
        Unbind(bound);
        bound = data;
        bound.OnHPChanged += hud.HealthChange;
        hud.Init(bound.MaxHP, bound.HP, bossName);
    }

    private void Unbind(NormalMonster data)
    {
        if (bound == null || data != bound) return;
        bound.OnHPChanged -= hud.HealthChange;
        bound = null;
    }
}
