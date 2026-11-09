using Enzeun.SO;

// 외부에서 상태 효과를 적용하거나 같은 출처의 효과를 제거할 수 있는 대상.
public interface IStatusEffectReceiver
{
    bool ApplyStatusEffect(StatusEffectData data, object source, int percentOverride = 0, float duration = 0f);
    int RemoveStatusEffects(object source);
}
