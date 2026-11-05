using Enzeun.SO;
using Sirenix.OdinInspector;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 런타임에 대상에게 걸려 있는 상태 효과 한 건입니다. (StatusEffectData 는 공유 설정값, 이 클래스는 걸린 상태)
    /// 남은 시간, 다음 틱까지의 시간처럼 변하는 값은 SO 가 아니라 여기에 둡니다. (SO 는 모든 사용자가 공유하므로 런타임 값을 쓰면 안 됨)
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

            // 틱이 필요한 모듈(체력 증감, 주기 행동)이 있을 때만 타이머를 만듭니다.
            // 0 으로 시작하므로 첫 틱은 걸리자마자(다음 갱신에) 발생합니다.
            for (int i = 0; i < data.modules.Count; i++)
            {
                if (data.modules[i] is HealthChangeModule || data.modules[i] is PeriodicActionModule)
                {
                    _tickTimers = new float[data.modules.Count];
                    HasTickModule = true;
                    break;
                }
            }
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
        /// 시간이 지나며 주기적으로 발동하는 모듈(체력 증감, 주기 행동)을 가지고 있는지 여부입니다.
        /// </summary>
        public bool HasTickModule { get; }

        // 모듈별 다음 틱까지 남은 시간입니다. 모듈은 공유 SO 라서 타이머는 이 인스턴스가 가집니다.
        private readonly float[] _tickTimers;

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

        /// <summary>
        /// 해당 모듈의 틱 타이머를 deltaTime 만큼 줄이고, 틱 시점이 되었는지 반환합니다.
        /// 한 번 호출에 최대 한 번만 true 입니다. (프레임이 길게 튀어도 밀린 틱을 몰아서 주지 않음)
        /// 간격이 0 이면 첫 틱 이후로는 다시 발생하지 않습니다. (한 번만)
        /// </summary>
        public bool ConsumeTick(int moduleIndex, float deltaTime, float interval)
        {
            if (_tickTimers == null)
            {
                return false;
            }

            _tickTimers[moduleIndex] -= deltaTime;

            if (_tickTimers[moduleIndex] > 0f)
            {
                return false;
            }

            _tickTimers[moduleIndex] = interval > 0f ? interval : float.PositiveInfinity;
            return true;
        }
    }
}
