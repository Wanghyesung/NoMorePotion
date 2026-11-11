using Enzeun.SO;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 상태창 밑에 미리 그려 둔 상태 효과 칸 하나입니다. 칸마다 보여 줄 효과(StatusEffectData)를 정해 두면,
    /// StatusEffectBarUI 가 그 효과가 몇 건 걸려 있는지 알려 주고 이 칸은 그에 맞게 보이거나 숨습니다.
    /// - 0 건이면 칸을 숨깁니다. (레이아웃 그룹이 자리를 당겨 줌)
    /// - 1 건 이상이면 아이콘을 보여 줍니다. 2 건 이상이면 개수(×2)를 함께 보여 줍니다.
    /// - 지속시간이 있는 효과는 아이콘 밑에 남은 시간(초)을 작게 보여 줍니다.
    /// 값이 그대로면 글자를 다시 쓰지 않아서 불필요한 갱신이 없습니다.
    /// </summary>
    public class StatusEffectIconView : MonoBehaviour
    {
        [SerializeField, LabelText("이 칸이 보여 줄 상태 효과"), Required("버프/디버프 SO 를 연결해 주세요. 같은 ID 의 효과가 이 칸에 묶입니다.")]
        private StatusEffectData _data;

        [SerializeField, LabelText("아이콘"), Required]
        private Image _icon;

        [SerializeField, LabelText("개수 글자 (선택)")]
        private TMP_Text _countText;

        [SerializeField, LabelText("남은 시간 글자 (선택)")]
        private TMP_Text _timeText;

        /// <summary>
        /// 이 칸이 보여 줄 상태 효과입니다.
        /// </summary>
        public StatusEffectData Data => _data;

        private string _countFormat = "×{0}";
        private string _timeFormat = "{0:0}초";

        // 마지막으로 그린 값입니다. 같은 값이면 다시 그리지 않습니다.
        private int _shownCount = -1;
        private int _shownSeconds = -1;
        private bool _timeVisible;

        /// <summary>
        /// 칸을 처음 상태로 준비합니다. (숨김) 효과에 아이콘이 없으면 기본 아이콘을 씁니다.
        /// </summary>
        /// <param name="defaultIcon">효과에 아이콘이 없을 때 쓸 기본 아이콘</param>
        /// <param name="countFormat">개수 글자 형식. {0} = 개수</param>
        /// <param name="timeFormat">남은 시간 글자 형식. {0} = 초</param>
        public void Initialize(Sprite defaultIcon, string countFormat, string timeFormat)
        {
            _countFormat = countFormat;
            _timeFormat = timeFormat;

            if (_icon != null)
            {
                Sprite sprite = _data != null && _data.statusEffectIcon != null ? _data.statusEffectIcon : defaultIcon;
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }

            _shownCount = -1;
            _shownSeconds = -1;
            _timeVisible = false;

            // 처음에는 프리팹에 켜져 있던 상태와 상관없이 글자를 모두 숨깁니다.
            if (_timeText != null)
            {
                _timeText.gameObject.SetActive(false);
            }

            if (_countText != null)
            {
                _countText.gameObject.SetActive(false);
            }

            SetActiveIfChanged(false);
        }

        /// <summary>
        /// 현재 걸린 건수와 남은 시간을 반영합니다.
        /// </summary>
        /// <param name="count">걸려 있는 건수. 0 이면 칸을 숨깁니다.</param>
        /// <param name="hasDuration">지속시간이 있는 효과인지 여부. false 면 남은 시간 글자를 숨깁니다.</param>
        /// <param name="remainingSeconds">남은 시간(초). 여러 건이면 가장 긴 것</param>
        public void SetState(int count, bool hasDuration, float remainingSeconds)
        {
            if (count <= 0)
            {
                SetActiveIfChanged(false);
                _shownCount = 0;
                return;
            }

            SetActiveIfChanged(true);

            if (count != _shownCount)
            {
                _shownCount = count;

                if (_countText != null)
                {
                    SetCountVisible(count > 1);

                    if (count > 1)
                    {
                        _countText.SetText(_countFormat, count);
                    }
                }
            }

            SetTimeVisible(hasDuration);

            if (hasDuration)
            {
                // 남은 시간이 0.1 초라도 "1초"로 보이도록 올림합니다.
                int seconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));

                if (seconds != _shownSeconds && _timeText != null)
                {
                    _shownSeconds = seconds;
                    _timeText.SetText(_timeFormat, seconds);
                }
            }
        }

        private void SetActiveIfChanged(bool active)
        {
            // 켜고 끌 때마다 레이아웃이 다시 계산되므로 값이 바뀔 때만 합니다.
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }

        private void SetCountVisible(bool visible)
        {
            if (_countText != null && _countText.gameObject.activeSelf != visible)
            {
                _countText.gameObject.SetActive(visible);
            }
        }

        private void SetTimeVisible(bool visible)
        {
            if (_timeText == null || _timeVisible == visible)
            {
                return;
            }

            _timeVisible = visible;
            _timeText.gameObject.SetActive(visible);

            if (!visible)
            {
                _shownSeconds = -1;
            }
        }
    }
}
