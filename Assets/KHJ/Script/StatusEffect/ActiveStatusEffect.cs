using Enzeun.SO;
using Sirenix.OdinInspector;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 런타임에 대상에게 걸려 있는 상태 효과 한 건입니다. (StatusEffectData 는 공유 설정값, 이 클래스는 걸린 상태)
    /// 남은 시간처럼 변하는 값은 SO 가 아니라 여기에 둡니다. (SO 는 모든 사용자가 공유하므로 런타임 값을 쓰면 안 됨)
    /// 저장하지 않는 런타임 상태라서 자동 프로퍼티로 두고, 인스펙터 확인용으로 [ShowInInspector] 를 붙입니다.
    /// </summary>
    public class ActiveStatusEffect
    {
        public ActiveStatusEffect(StatusEffectData data, object source, int percentOverride, float duration)
        {
            this.data = data;
            this.source = source;
            this.percentOverride = percentOverride;
            this.duration = duration;
            remaining = duration;
        }

        [ShowInInspector, ReadOnly, LabelText("상태 효과")]
        public StatusEffectData data { get; }

        // 이 효과를 건 주체입니다. (예: 체온 구간, 포션). 주체 단위로 한꺼번에 제거할 때 사용합니다.
        [ShowInInspector, ReadOnly, LabelText("건 주체")]
        public object source { get; }

        // 포션 등이 적용 시점에 넘기는 수치(%)입니다. 0 이면 SO 에 설정된 값을 사용합니다.
        [ShowInInspector, ReadOnly, LabelText("수치 덮어쓰기 (%)")]
        public int percentOverride { get; }

        [ShowInInspector, ReadOnly, LabelText("지속시간 (초)")]
        public float duration { get; }

        [ShowInInspector, ReadOnly, LabelText("남은 시간 (초)")]
        public float remaining { get; private set; }

        /// <summary>
        /// 지속시간이 있는 효과인지 여부입니다. SO 의 설정을 따릅니다.
        /// </summary>
        public bool hasDuration => data.hasDuration;

        /// <summary>
        /// 지속시간이 있는 효과가 시간이 다 되어 만료되었는지 여부입니다.
        /// </summary>
        public bool isExpired => hasDuration && remaining <= 0f;

        /// <summary>
        /// 남은 시간을 deltaTime 만큼 줄입니다. 지속시간이 없는 효과는 변하지 않습니다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!hasDuration)
            {
                return;
            }

            remaining -= deltaTime;
        }

        /// <summary>
        /// 남은 시간을 처음 지속시간으로 되돌립니다.
        /// </summary>
        public void RefreshDuration()
        {
            remaining = duration;
        }
    }
}
