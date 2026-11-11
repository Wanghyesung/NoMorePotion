using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 내 플레이어에게 걸린 버프/디버프를 상태창 밑에 아이콘으로 보여 주는 UI 입니다.
    /// 칸은 UI 에 미리 그려 두고(StatusEffectIconView), 칸마다 보여 줄 효과(StatusEffectData)와 순서를 직접 정합니다.
    /// - 같은 효과 ID(예: 60001)는 한 칸에 묶고, 2 건 이상 걸리면 개수를 함께 보여 줍니다.
    /// - 걸린 것이 0 건인 칸은 숨깁니다. (보여 줄 칸이 없는 효과가 걸리면 경고만 남기고 건너뜀)
    /// - 버프와 디버프는 각각 한 줄입니다. 칸 목록의 순서가 왼쪽부터 보이는 순서이므로, 지속시간 없는 버프를 앞쪽에 두세요.
    /// - 남은 시간은 아이콘 밑에 초 단위로 작게 보여 주고, 1초에 한 번만 갱신합니다.
    /// 추가/제거는 BuffContainer 의 이벤트로 바로 반영하고, 시간 표시만 주기적으로 읽습니다.
    /// (지속시간이 갱신될 때는 이벤트가 없으므로 시간은 이벤트가 아니라 주기적으로 읽어야 합니다.)
    /// 컨테이너는 씬에 있는 SurviveStatusManager 가 가진 내 플레이어 전용 컨테이너라서, 플레이어가 생성되기를 기다리지 않고 매니저가 준비되면 연결합니다.
    /// </summary>
    public class StatusEffectBarUI : MonoBehaviour
    {
        [SerializeField, LabelText("버프 칸 (왼쪽부터 보일 순서)"), BoxGroup("버프")]
        private List<StatusEffectIconView> _buffSlots = new List<StatusEffectIconView>();

        [SerializeField, LabelText("디버프 칸 (왼쪽부터 보일 순서)"), BoxGroup("디버프")]
        private List<StatusEffectIconView> _debuffSlots = new List<StatusEffectIconView>();

        [SerializeField, LabelText("기본 아이콘 (아이콘이 없는 효과)"), BoxGroup("표시")]
        private Sprite _defaultIcon;

        [SerializeField, LabelText("개수 형식 ({0} = 개수)"), BoxGroup("표시")]
        private string _countFormat = "×{0}";

        [SerializeField, LabelText("남은 시간 형식 ({0} = 초)"), BoxGroup("표시")]
        private string _timeFormat = "{0:0}초";

        [SerializeField, LabelText("남은 시간 갱신 주기 (초)"), BoxGroup("표시"), MinValue(0.1f)]
        private float _timerIntervalSeconds = 1f;

        private BuffContainer _container;
        private CancellationTokenSource _cts;

        // 모든 칸(버프 다음 디버프)과 효과 ID → 칸 번호입니다. Awake 에서 한 번만 만듭니다.
        private readonly List<StatusEffectIconView> _slots = new List<StatusEffectIconView>();
        private readonly Dictionary<int, int> _slotIndexById = new Dictionary<int, int>();

        // 갱신할 때 쓰는 작업 공간입니다. 칸 수만큼 한 번만 만들어 재사용합니다. (갱신마다 할당 없음)
        private int[] _counts;
        private float[] _maxRemaining;
        private bool[] _hasDuration;

        // 지속시간이 있는 효과가 보이는 중인지 여부입니다. 없으면 시간 갱신을 건너뜁니다.
        private bool _hasTimed;

        // 칸이 없는 효과를 경고한 적 있는지 기록합니다. (같은 경고를 반복하지 않음)
        private readonly HashSet<int> _warnedMissing = new HashSet<int>();

        private void Awake()
        {
            RegisterSlots(_buffSlots);
            RegisterSlots(_debuffSlots);

            _counts = new int[_slots.Count];
            _maxRemaining = new float[_slots.Count];
            _hasDuration = new bool[_slots.Count];

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Initialize(_defaultIcon, _countFormat, _timeFormat);
            }
        }

        private void OnEnable()
        {
            _cts = new CancellationTokenSource();
            BindWhenReadyAsync(_cts.Token).Forget();
            TimerLoopAsync(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            Unbind();
            HideAll();
        }

        // 칸 목록을 등록합니다. 효과가 연결되지 않았거나 같은 효과가 두 칸에 있으면 경고하고 그 칸은 건너뜁니다.
        private void RegisterSlots(List<StatusEffectIconView> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                StatusEffectIconView slot = slots[i];

                if (slot == null)
                {
                    continue;
                }

                if (slot.Data == null)
                {
                    Debug.LogWarning($"[StatusEffectBarUI] : 칸 '{slot.name}' 에 상태 효과가 연결되지 않았습니다.", slot);
                    continue;
                }

                int id = slot.Data.statusEffectID;

                if (_slotIndexById.ContainsKey(id))
                {
                    Debug.LogWarning($"[StatusEffectBarUI] : 상태 효과({id}) 가 두 칸에 연결되어 있습니다. 첫 번째 칸만 사용합니다.", slot);
                    continue;
                }

                _slotIndexById.Add(id, _slots.Count);
                _slots.Add(slot);
            }
        }

        /// <summary>
        /// 내 플레이어의 컨테이너가 준비될 때까지 기다렸다가 이벤트를 연결하고, 이미 걸려 있는 효과를 한 번 그립니다.
        /// (게임 시작 시 정신력 구간으로 걸린 효과 등은 이벤트로 오지 않으므로 연결할 때 현재 목록으로 그려야 함)
        /// </summary>
        private async UniTaskVoid BindWhenReadyAsync(CancellationToken token)
        {
            bool canceled = await UniTask.WaitUntil(
                    () => SurviveStatusManager.Instance != null && SurviveStatusManager.Instance.PlayerBuffContainer != null,
                    cancellationToken: token)
                .SuppressCancellationThrow();

            if (canceled)
            {
                return;
            }

            _container = SurviveStatusManager.Instance.PlayerBuffContainer;
            _container.OnEffectAdded += HandleEffectChanged;
            _container.OnEffectRemoved += HandleEffectChanged;

            Refresh();
        }

        private void Unbind()
        {
            if (_container != null)
            {
                _container.OnEffectAdded -= HandleEffectChanged;
                _container.OnEffectRemoved -= HandleEffectChanged;
            }

            _container = null;
        }

        private void HandleEffectChanged(ActiveStatusEffect effect)
        {
            Refresh();
        }

        /// <summary>
        /// 일정 주기마다 남은 시간을 다시 읽어 표시합니다. 지속시간이 있는 효과가 없으면 아무것도 하지 않습니다.
        /// </summary>
        private async UniTaskVoid TimerLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(_timerIntervalSeconds), ignoreTimeScale: true, cancellationToken: token)
                    .SuppressCancellationThrow();

                if (canceled)
                {
                    return;
                }

                if (_hasTimed)
                {
                    Refresh();
                }
            }
        }

        /// <summary>
        /// 컨테이너의 현재 목록으로 모든 칸을 다시 그립니다. 같은 ID 는 건수를 세고, 남은 시간은 가장 긴 것을 씁니다.
        /// </summary>
        private void Refresh()
        {
            if (_container == null || _counts == null)
            {
                return;
            }

            Array.Clear(_counts, 0, _counts.Length);
            Array.Clear(_maxRemaining, 0, _maxRemaining.Length);
            Array.Clear(_hasDuration, 0, _hasDuration.Length);

            Collect(_container.buffs);
            Collect(_container.debuffs);

            bool anyTimed = false;

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].SetState(_counts[i], _hasDuration[i], _maxRemaining[i]);

                if (_counts[i] > 0 && _hasDuration[i])
                {
                    anyTimed = true;
                }
            }

            _hasTimed = anyTimed;
        }

        private void Collect(IReadOnlyList<ActiveStatusEffect> effects)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                ActiveStatusEffect effect = effects[i];

                if (effect == null || effect.data == null)
                {
                    continue;
                }

                int id = effect.data.statusEffectID;

                if (!_slotIndexById.TryGetValue(id, out int index))
                {
                    WarnMissingSlot(id, effect.data.statusEffectName);
                    continue;
                }

                _counts[index]++;

                if (effect.hasDuration)
                {
                    _hasDuration[index] = true;

                    if (effect.remaining > _maxRemaining[index])
                    {
                        _maxRemaining[index] = effect.remaining;
                    }
                }
            }
        }

        private void WarnMissingSlot(int id, string effectName)
        {
            if (_warnedMissing.Add(id))
            {
                Debug.LogWarning($"[StatusEffectBarUI] : 상태 효과 '{effectName}'({id}) 를 보여 줄 칸이 없어 표시하지 않습니다. 버프/디버프 칸 목록에 추가해 주세요.");
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].SetState(0, false, 0f);
            }

            _hasTimed = false;
        }
    }
}
