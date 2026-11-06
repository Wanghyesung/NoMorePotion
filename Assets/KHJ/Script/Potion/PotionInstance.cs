using Enzeun.SO;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 제작되어 뱃속(인벤토리)에 들어가는 포션 한 개의 런타임 데이터입니다. (SO 가 아니라 제작할 때마다 새로 생성)
    /// 제작한 포션의 데이터(<see cref="Data"/>)와, 제작 판정에서 실패했을 때 붙는 함정 키워드(<see cref="TrapKeyword"/>)를 가집니다.
    /// 같은 포션이어도 함정 여부가 다를 수 있으므로 SO 가 아니라 인스턴스마다 따로 보관합니다.
    ///
    /// 함정 포션 규칙 (기획)
    /// - 제작하는 순간의 정신력으로 성공/실패를 판정하고, 실패하면 함정 포션이 됩니다.
    /// - 함정 포션은 원래 포션에 함정 키워드가 추가된 것이라, 폭발 범위는 원래 포션을 따르고 효과는 키워드의 것이 더해집니다.
    /// - 마시기 전에 R 을 누르는 동안 흔들어서(<see cref="Inspect"/>) 함정인지와 키워드를 확인할 수 있고, R 을 떼면 다시 보이지 않습니다.
    /// </summary>
    public sealed class PotionInstance
    {
        /// <param name="data">제작한 포션 데이터</param>
        /// <param name="trapKeyword">함정 키워드(5만번대 키워드 SO). 정상 포션이면 null</param>
        public PotionInstance(PotionData data, TrapPotionKeyword trapKeyword = null)
        {
            Data = data;
            TrapKeyword = trapKeyword;
        }

        /// <param name="instanceId">이미 정해진 인스턴스 ID. 네트워크로 받은 포션을 복원할 때 사용합니다.</param>
        public PotionInstance(PotionData data, TrapPotionKeyword trapKeyword, int instanceId)
            : this(data, trapKeyword)
        {
            InstanceId = instanceId;
        }

        /// <summary>
        /// 포션 한 개를 구분하는 고유 ID 입니다. 0 이면 아직 정해지지 않은 상태입니다.
        /// 같은 포션 종류여도 인스턴스마다 다르고, 보관함이 바뀌어도 유지됩니다. (네트워크에서는 "이 포션을 꺼내 달라"를 이 ID 로 요청)
        /// ID 는 한 곳(권한을 가진 쪽)에서만 정합니다. <see cref="PotionInstanceIds"/>
        /// </summary>
        public int InstanceId { get; private set; }

        /// <summary>
        /// 인스턴스 ID 를 정합니다. 이미 정해진 포션은 바꿀 수 없습니다.
        /// </summary>
        /// <returns>정해졌으면 true</returns>
        public bool AssignInstanceId(int instanceId)
        {
            if (InstanceId != 0 || instanceId <= 0)
            {
                return false;
            }

            InstanceId = instanceId;
            return true;
        }

        /// <summary>
        /// 제작한 원래 포션 데이터입니다. 폭발 범위와 기본 효과의 기준입니다.
        /// </summary>
        public PotionData Data { get; }

        /// <summary>
        /// 함정 키워드입니다. 정상 포션이면 null 입니다. (확인 전에도 값은 들어 있고, 외부에는 Inspect 로 알려 줌)
        /// </summary>
        public TrapPotionKeyword TrapKeyword { get; }

        /// <summary>
        /// 함정 포션인지 여부입니다. 실제 값이므로 플레이어에게 보여 줄 때는 Inspect 를 사용합니다.
        /// </summary>
        public bool IsTrap => TrapKeyword != null;

        /// <summary>
        /// 포션을 흔들어서 함정인지와 키워드를 알려 줍니다. 조회만 하며 "확인했다"는 상태는 저장하지 않습니다.
        /// R 을 누르는 동안만 보여 주고 떼면 숨기는 처리는 호출하는 쪽(입력/UI)이 맡습니다.
        /// </summary>
        /// <param name="trapKeyword">함정이면 함정 키워드, 정상 포션이면 null</param>
        /// <returns>함정 포션이면 true</returns>
        public bool Inspect(out TrapPotionKeyword trapKeyword)
        {
            trapKeyword = TrapKeyword;
            return IsTrap;
        }
    }
}
