using System;
using System.Collections.Generic;
using Enzeun.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.SO
{
    /// <summary>
    /// 생존 상태 구간 하나에 걸리는 상태 효과(버프/디버프) 목록입니다.
    /// </summary>
    [Serializable]
    public class StateEffects<TState> where TState : struct, Enum
    {
        [SerializeField, LabelText("구간")]
        private TState _state;
        public TState State => _state;

        [SerializeField, LabelText("걸리는 효과")]
        private List<StatusEffectData> _effects = new List<StatusEffectData>();
        public IReadOnlyList<StatusEffectData> Effects => _effects;
    }

    /// <summary>
    /// 생존 상태(체온, 정신력, 수분, 포만감)의 구간별로 어떤 버프/디버프가 걸리는지를 정의하는 표입니다.
    /// 구간이 바뀌면 이전 구간의 효과를 모두 빼고 새 구간의 효과를 모두 겁니다. 한 구간의 효과는 동시에 적용됩니다.
    /// 예) 수분 Dehydrated : 갈증 + 탈수 / 정신력 GoToDie : 멍청함 + 뭐라고? + 혼란 + 죽어라
    /// </summary>
    [CreateAssetMenu(fileName = "SurviveStatusEffectTable", menuName = "Scriptable Objects/SurviveStatusEffectTable")]
    public class SurviveStatusEffectTable : ScriptableObject
    {
        [SerializeField, LabelText("체온"), BoxGroup("구간별 효과")]
        private List<StateEffects<BodyTemperatureState>> _bodyTemperature = new List<StateEffects<BodyTemperatureState>>();

        [SerializeField, LabelText("정신력"), BoxGroup("구간별 효과")]
        private List<StateEffects<MentalState>> _mentalStrength = new List<StateEffects<MentalState>>();

        [SerializeField, LabelText("수분"), BoxGroup("구간별 효과")]
        private List<StateEffects<HydrationState>> _hydration = new List<StateEffects<HydrationState>>();

        [SerializeField, LabelText("포만감"), BoxGroup("구간별 효과")]
        private List<StateEffects<SatiationState>> _satiation = new List<StateEffects<SatiationState>>();

        public IReadOnlyList<StatusEffectData> GetEffects(BodyTemperatureState state) => Find(_bodyTemperature, state);
        public IReadOnlyList<StatusEffectData> GetEffects(MentalState state) => Find(_mentalStrength, state);
        public IReadOnlyList<StatusEffectData> GetEffects(HydrationState state) => Find(_hydration, state);
        public IReadOnlyList<StatusEffectData> GetEffects(SatiationState state) => Find(_satiation, state);

        /// <summary>
        /// 해당 구간의 효과 목록을 찾습니다. 표에 없는 구간은 효과 없음(null)입니다.
        /// </summary>
        private static IReadOnlyList<StatusEffectData> Find<TState>(List<StateEffects<TState>> table, TState state)
            where TState : struct, Enum
        {
            for (int i = 0; i < table.Count; i++)
            {
                if (EqualityComparer<TState>.Default.Equals(table[i].State, state))
                {
                    return table[i].Effects;
                }
            }

            return null;
        }
    }
}
