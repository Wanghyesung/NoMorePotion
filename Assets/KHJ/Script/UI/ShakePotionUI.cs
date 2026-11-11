using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Enzeun.Runtime;
using Enzeun.SO;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 포션을 흔들 때(R 을 누르는 동안) 양손에 든 포션의 아이콘, 이름, 함정 여부를 보여 주는 UI 입니다.
/// 플레이어(Hand)는 흔들기 시작(OnShake)과 종료(OnShakeFinished)만 알려 주고, 함정인지 확인하는 것(Inspect)과 화면에 보여 주는 것은 이 UI 가 맡습니다.
/// - 시작 : 포션이 있는 손의 칸만 나타납니다. 칸마다 아이콘, 포션 이름, 그 밑에 작게 함정 키워드를 보여 줍니다. (정상 포션은 키워드 칸이 비어 있음)
/// - 종료 : R 을 떼거나 흔들기가 중단되면 칸이 사라집니다.
/// 나타날 때는 빠르게, 사라질 때는 천천히 CanvasGroup 알파를 DOTween 으로 바꿉니다. DOTween 은 지금 알파에서 이어서 시작하므로,
/// 나타나는 도중에 R 을 놓거나 사라지는 도중에 다시 눌러도 알파가 튀지 않습니다.
/// 사라지는 페이드가 끝나면 글자와 아이콘도 비웁니다. (한 번 확인했다는 상태는 저장하지 않음)
/// 칸은 항상 고정 위치이고 클릭 등 상호작용은 없으므로, 레이캐스트는 처음부터 꺼 둡니다.
/// 플레이어는 게임 중에 생성되므로, 내 플레이어를 찾을 때까지 기다렸다가 연결합니다.
/// </summary>
public class ShakePotionUI : MonoBehaviour
{
    [SerializeField, LabelText("왼손 칸"), BoxGroup("왼손"), Required("왼손 포션 칸을 연결해 주세요.")]
    private CanvasGroup _leftGroup;

    [SerializeField, LabelText("왼손 아이콘"), BoxGroup("왼손"), Required]
    private Image _leftIcon;

    [SerializeField, LabelText("왼손 포션 이름"), BoxGroup("왼손"), Required]
    private TMP_Text _leftNameText;

    [SerializeField, LabelText("왼손 함정 키워드"), BoxGroup("왼손"), Required]
    private TMP_Text _leftTrapText;




    [SerializeField, LabelText("오른손 칸"), BoxGroup("오른손"), Required("오른손 포션 칸을 연결해 주세요.")]
    private CanvasGroup _rightGroup;

    [SerializeField, LabelText("오른손 아이콘"), BoxGroup("오른손"), Required]
    private Image _rightIcon;

    [SerializeField, LabelText("오른손 포션 이름"), BoxGroup("오른손"), Required]
    private TMP_Text _rightNameText;

    [SerializeField, LabelText("오른손 함정 키워드"), BoxGroup("오른손"), Required]
    private TMP_Text _rightTrapText;


    [SerializeField, LabelText("왼손 이미지"), BoxGroup("현재 플레이어 손"), Required("왼손 이미지의 RectTransform 을 연결해 주세요.")]
    private RectTransform _leftHandImage;

    [SerializeField, LabelText("오른손 이미지"), BoxGroup("현재 플레이어 손"), Required("오른손 이미지의 RectTransform 을 연결해 주세요.")]
    private RectTransform _rightHandImage;

    [SerializeField, LabelText("선택된 손 크기 (배)"), BoxGroup("현재 플레이어 손"), MinValue(0f)]
    private float _selectedHandScale = 1.25f;

    [SerializeField, LabelText("선택 안 된 손 크기 (배)"), BoxGroup("현재 플레이어 손"), MinValue(0f)]
    private float _normalHandScale = 1f;

    [SerializeField, LabelText("크기 바뀌는 시간 (초)"), BoxGroup("현재 플레이어 손"), MinValue(0f)]
    private float _handScaleDuration = 0.15f;

    [SerializeField, LabelText("크기 곡선"), BoxGroup("현재 플레이어 손")]
    private Ease _handScaleEase = Ease.OutBack;



    [SerializeField, LabelText("나타나는 시간 (초)"), BoxGroup("페이드"), MinValue(0f)]
    private float _showDuration = 0.12f;

    [SerializeField, LabelText("나타날 때 곡선"), BoxGroup("페이드")]
    private Ease _showEase = Ease.OutQuad;

    [SerializeField, LabelText("사라지는 시간 (초)"), BoxGroup("페이드"), MinValue(0f)]
    private float _hideDuration = 0.6f;

    [SerializeField, LabelText("사라질 때 곡선"), BoxGroup("페이드")]
    private Ease _hideEase = Ease.InOutSine;

    // 함정 키워드를 보여 주는 형식입니다. {0} = 키워드 이름 (예: "(달팽이)"). 정상 포션은 이 칸을 비웁니다.
    [SerializeField, LabelText("함정 키워드 형식 ({0} = 키워드 이름)"), BoxGroup("형식")]
    private string _trapFormat = "({0})";

    // 내 플레이어를 찾는 주기입니다. (플레이어는 게임 시작 후에 생성됨)
    private const int FindIntervalMilliseconds = 500;

    private Hand playerHand;
    private bool isBound;
    private CancellationTokenSource bindCts;

    private Slot leftSlot;
    private Slot rightSlot;

    /// <summary>
    /// 한 손의 포션 칸입니다. 칸이 지금 "보이려는" 상태인지 기억해서, 이미 같은 방향이면 페이드를 다시 시작하지 않습니다.
    /// 페이드는 항상 지금 알파에서 이어서 시작하고, 새 페이드를 시작하기 전에 이전 페이드를 끝냅니다(Kill).
    /// </summary>
    private sealed class Slot
    {
        private readonly CanvasGroup group;
        private readonly Image icon;
        private readonly TMP_Text nameText;
        private readonly TMP_Text trapText;
        private readonly string trapFormat;
        private readonly float showDuration;
        private readonly Ease showEase;
        private readonly float hideDuration;
        private readonly Ease hideEase;
        private bool shown;
        private Tween fade;

        public Slot(CanvasGroup group, Image icon, TMP_Text nameText, TMP_Text trapText, string trapFormat,
            float showDuration, Ease showEase, float hideDuration, Ease hideEase)
        {
            this.group = group;
            this.icon = icon;
            this.nameText = nameText;
            this.trapText = trapText;
            this.trapFormat = trapFormat;
            this.showDuration = showDuration;
            this.showEase = showEase;
            this.hideDuration = hideDuration;
            this.hideEase = hideEase;

            // 상호작용이 없는 UI 라서 처음부터 클릭과 포커스를 받지 않게 한 번만 꺼 둡니다.
            if (group != null)
            {
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 포션 내용으로 채우고 칸을 나타냅니다. 이미 보이는 중이면 내용만 바꿉니다.
        /// </summary>
        public void Show(PotionInstance potion)
        {
            // 아이콘이 없는 포션은 sprite 가 비어 있는 Image 가 흰 사각형으로 보이지 않도록 Image 를 끕니다.
            icon.sprite = potion.Data.potionIcon;
            icon.enabled = icon.sprite != null;
            nameText.SetText(potion.Data.potionName);

            // 함정인지는 Inspect 로만 확인합니다. (IsTrap 같은 실제 값을 직접 읽지 않음) 정상 포션은 키워드 칸을 비웁니다.
            bool isTrap = potion.Inspect(out TrapPotionKeyword keyword);
            trapText.SetText(isTrap && keyword != null ? string.Format(trapFormat, keyword.KeywordName) : string.Empty);

            if (shown)
            {
                return;
            }

            shown = true;
            FadeTo(1f, showDuration, showEase, null);
        }

        /// <summary>
        /// 칸을 천천히 사라지게 합니다. 이미 숨는 중이거나 숨은 칸이면 아무것도 하지 않습니다.
        /// 페이드가 끝나면 아이콘과 글자도 비웁니다. (끝나기 전에 다시 나타나면 이 정리는 일어나지 않음)
        /// </summary>
        public void Hide()
        {
            if (!shown)
            {
                return;
            }

            shown = false;
            FadeTo(0f, hideDuration, hideEase, ClearContent);
        }

        /// <summary>
        /// 애니메이션 없이 즉시 숨기고 내용도 비웁니다. 시작할 때와 UI 가 꺼질 때 사용합니다.
        /// </summary>
        public void HideImmediate()
        {
            shown = false;
            fade?.Kill();
            fade = null;

            if (group != null)
            {
                group.alpha = 0f;
            }

            ClearContent();
        }

        // 지금 알파에서 목표 알파까지 페이드합니다. 이전 페이드는 끝내고, 시간은 Time.timeScale 의 영향을 받지 않게 합니다.
        private void FadeTo(float target, float duration, Ease ease, TweenCallback onComplete)
        {
            if (group == null)
            {
                return;
            }

            fade?.Kill();

            fade = group.DOFade(target, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .SetLink(group.gameObject);

            if (onComplete != null)
            {
                fade.OnComplete(onComplete);
            }
        }

        private void ClearContent()
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (nameText != null)
            {
                nameText.SetText(string.Empty);
            }

            if (trapText != null)
            {
                trapText.SetText(string.Empty);
            }
        }
    }

    private void Awake()
    {
        leftSlot = new Slot(_leftGroup, _leftIcon, _leftNameText, _leftTrapText, _trapFormat, _showDuration, _showEase, _hideDuration, _hideEase);
        rightSlot = new Slot(_rightGroup, _rightIcon, _rightNameText, _rightTrapText, _trapFormat, _showDuration, _showEase, _hideDuration, _hideEase);

        leftSlot.HideImmediate();
        rightSlot.HideImmediate();

        if (_leftHandImage != null)
        {
            _leftHandBaseScale = _leftHandImage.localScale;
        }

        if (_rightHandImage != null)
        {
            _rightHandBaseScale = _rightHandImage.localScale;
        }
    }

    private void OnEnable()
    {
        bindCts = new CancellationTokenSource();
        BindWhenReadyAsync(bindCts.Token).Forget();
    }

    private void OnDisable()
    {
        bindCts?.Cancel();
        bindCts?.Dispose();
        bindCts = null;

        UnBindEvent();
        ResetHandIndicator();

        // 꺼질 때는 페이드를 기다리지 않고 바로 숨깁니다.
        leftSlot?.HideImmediate();
        rightSlot?.HideImmediate();
    }

    /// <summary>
    /// 내 플레이어의 Hand 를 찾을 때까지 기다렸다가 이벤트를 연결합니다.
    /// </summary>
    private async UniTaskVoid BindWhenReadyAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Hand hand = FindLocalHand();

            if (hand != null)
            {
                playerHand = hand;
                BindEvent();
                return;
            }

            bool canceled = await UniTask.Delay(FindIntervalMilliseconds, ignoreTimeScale: true, cancellationToken: token)
                .SuppressCancellationThrow();

            if (canceled)
            {
                return;
            }
        }
    }

    // 씬의 플레이어 중 내 것(로컬)의 Hand 를 찾습니다. 다른 사람의 플레이어는 건너뜁니다.
    private static Hand FindLocalHand()
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].IsLocalPlayer && players[i].Hand != null)
            {
                return players[i].Hand;
            }
        }

        return null;
    }

    private void BindEvent()
    {
        if (playerHand == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[ShakePotionUI] : Player.Hand 를 찾을 수 없어 연결하지 못했습니다.");
#endif
            return;
        }

        if (isBound)
        {
            return;
        }

        playerHand.OnShake += ShowResult;
        playerHand.OnShakeFinished += HideShake;
        playerHand.OnFocusChanged += ChangeHandSide;
        isBound = true;

        // OnFocusChanged 는 손이 바뀔 때만 발생하므로, 처음 연결될 때 지금 쓰는 손을 직접 반영합니다.
        ChangeHandSide((int)playerHand.FocusedSide);
    }

    private void UnBindEvent()
    {
        if (!isBound)
        {
            return;
        }

        // 플레이어가 이미 사라졌으면(Unity 오브젝트가 null) 이벤트도 함께 사라졌으므로 해제할 것이 없습니다.
        if (playerHand != null)
        {
            playerHand.OnShake -= ShowResult;
            playerHand.OnShakeFinished -= HideShake;
            playerHand.OnFocusChanged -= ChangeHandSide;
        }

        isBound = false;
        ResetHandIndicator();
    }

    /// <summary>
    /// 흔들기를 시작할 때 양손의 포션을 확인해서 보여 줍니다. 포션이 있는 손의 칸만 나타나고, 비어 있는 손(null)의 칸은 그대로 숨어 있습니다.
    /// (두 손 모두 비어 있으면 흔들기 자체가 시작되지 않지만, 혹시 오면 아무것도 보여 주지 않습니다.)
    /// 정상 포션은 "으쌰으쌰 포션 (소)", 함정 포션은 이름 밑에 "(달팽이)" 같은 키워드가 함께 보입니다.
    /// </summary>
    public void ShowResult(PotionInstance leftPotion, PotionInstance rightPotion)
    {
        ShowSlot(leftSlot, leftPotion);
        ShowSlot(rightSlot, rightPotion);
    }

    /// <summary>
    /// 흔들기가 끝나면(정상 완료, 중단 모두) 보이던 칸을 사라지게 합니다.
    /// </summary>
    public void HideShake()
    {
        leftSlot?.Hide();
        rightSlot?.Hide();
    }

    private static void ShowSlot(Slot slot, PotionInstance potion)
    {
        if (slot == null)
        {
            return;
        }

        if (potion == null || potion.Data == null)
        {
            slot.Hide();
            return;
        }

        slot.Show(potion);
    }

    // 지금 선택된 손입니다. 0 = 왼손, 1 = 오른손, -1 = 아직 반영하지 않음
    private int _currentHandSide = -1;
    private Tween _leftHandTween;
    private Tween _rightHandTween;

    // 씬에 배치된 원래 크기입니다. 크기를 키울 때 이 값의 배수로 계산해서, 원래 크기가 1 이 아니어도 맞습니다.
    private Vector3 _leftHandBaseScale = Vector3.one;
    private Vector3 _rightHandBaseScale = Vector3.one;

    /// <summary>
    /// 지금 쓰는 손을 바꿉니다. 선택된 손의 이미지는 키우고, 선택 안 된 손은 기본 크기로 돌립니다. (두 이미지는 항상 보임)
    /// DOScale 은 지금 크기에서 이어서 시작하므로 손을 빠르게 연타로 바꿔도 크기가 튀지 않습니다.
    /// </summary>
    private void ChangeHandSide(int side)
    {
        if (side == _currentHandSide)
        {
            return;
        }

        _currentHandSide = side;

        ScaleHand(_leftHandImage, _leftHandBaseScale, ref _leftHandTween, side == 0 ? _selectedHandScale : _normalHandScale);
        ScaleHand(_rightHandImage, _rightHandBaseScale, ref _rightHandTween, side == 0 ? _normalHandScale : _selectedHandScale);
    }

    // 이전 크기 변화는 끝내고, 지금 크기에서 목표 크기(원래 크기의 factor 배)까지 키우거나 줄입니다. (Time.timeScale 의 영향을 받지 않음)
    private void ScaleHand(RectTransform target, Vector3 baseScale, ref Tween tween, float factor)
    {
        if (target == null)
        {
            return;
        }

        tween?.Kill();

        tween = target.DOScale(baseScale * factor, _handScaleDuration)
            .SetEase(_handScaleEase)
            .SetUpdate(true)
            .SetLink(target.gameObject);
    }

    // 연결이 끊기거나 UI 가 꺼질 때 두 손을 기본 크기로 바로 되돌립니다. (다시 켜면 현재 손을 새로 반영)
    private void ResetHandIndicator()
    {
        _leftHandTween?.Kill();
        _rightHandTween?.Kill();
        _leftHandTween = null;
        _rightHandTween = null;

        if (_leftHandImage != null)
        {
            _leftHandImage.localScale = _leftHandBaseScale * _normalHandScale;
        }

        if (_rightHandImage != null)
        {
            _rightHandImage.localScale = _rightHandBaseScale * _normalHandScale;
        }

        _currentHandSide = -1;
    }
}
