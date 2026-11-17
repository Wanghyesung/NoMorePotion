using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 아이콘의 왼쪽 클릭을 알려 주고, 눌렸을 때 아이콘 이미지가 변하는 연출을 보여 주는 도우미입니다.
    /// InventoryTransferRelay 가 개인 인벤 칸(InventoryIcon 에 Button 이 연결되지 않아 OnClicked 가 오지 않는 칸)에 자동으로 붙입니다.
    /// 같은 오브젝트의 InventoryIcon 을 고치지 않고, 클릭 이벤트만 따로 받습니다. 오른쪽 클릭은 알리지 않습니다. (InventoryIcon 이 조합창 올리기로 씀)
    ///
    /// 눌림 연출 (Button 의 Transition 을 대신함)
    /// - 눌림 이미지를 지정하면 : 누르는 동안 이미지를 그 이미지로 바꿨다가 뗄 때 원래 이미지로 되돌립니다. (Sprite Swap)
    /// - 지정하지 않으면 : 공용 인벤 아이콘(ItemIcon 프리팹의 Button)과 같이 누르는 동안 색을 어둡게(78%) 했다가 0.1초에 걸쳐 되돌립니다. (Color Tint)
    /// 연출 대상은 같은 오브젝트의 Image 입니다. 없으면 클릭만 알립니다.
    /// </summary>
    public sealed class InventoryIconClickProxy : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        #region Config
        // ItemIcon 프리팹의 Button(Color Tint) 눌림 색과 같은 값입니다.
        private static readonly Color PressedTint = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
        private const float FadeSeconds = 0.1f;

        private Sprite _pressedSprite;

        /// <summary>
        /// 눌림 이미지를 정합니다. null 이면 색을 어둡게 하는 연출을 씁니다.
        /// </summary>
        public void Configure(Sprite pressedSprite)
        {
            _pressedSprite = pressedSprite;
        }
        #endregion

        #region Event
        /// <summary>
        /// 이 아이콘을 왼쪽 클릭했을 때 그 아이콘과 함께 발생합니다.
        /// </summary>
        public event Action<InventoryIcon> LeftClicked;

        private InventoryIcon _icon;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

#if UNITY_EDITOR
            Debug.Log($"[InventoryIconClickProxy] : '{name}' 왼쪽 클릭을 받았습니다. (구독자 {(LeftClicked != null ? LeftClicked.GetInvocationList().Length : 0)}명)", this);
#endif

            if (_icon == null)
            {
                _icon = GetComponent<InventoryIcon>();
            }

            if (_icon != null)
            {
                LeftClicked?.Invoke(_icon);
            }
        }
        #endregion

        #region Press
        private Image _target;
        private Sprite _normalSprite;
        private bool _pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                SetPressed(true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            SetPressed(false);
        }

        // 누른 채로 칸 밖으로 나가면 눌림을 풉니다. (Button 과 같은 동작)
        public void OnPointerExit(PointerEventData eventData)
        {
            SetPressed(false);
        }

        // 눌린 채로 꺼져도(인벤토리를 닫는 등) 다음에 켜졌을 때 눌린 모양이 남지 않게 합니다.
        private void OnDisable()
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;

            if (_target == null)
            {
                return;
            }

            if (_pressedSprite != null)
            {
                _target.sprite = _normalSprite;
            }
            else
            {
                _target.canvasRenderer.SetColor(Color.white);
            }
        }

        private void SetPressed(bool pressed)
        {
            if (_pressed == pressed)
            {
                return;
            }

            if (_target == null)
            {
                _target = GetComponent<Image>();
            }

            if (_target == null)
            {
                return;
            }

            _pressed = pressed;

            if (_pressedSprite != null)
            {
                if (pressed)
                {
                    _normalSprite = _target.sprite;
                    _target.sprite = _pressedSprite;
                }
                else
                {
                    _target.sprite = _normalSprite;
                }

                return;
            }

            // Button 의 Color Tint 와 같은 방식입니다. (그래픽 색에 곱해지는 렌더러 색을 시간 배율과 무관하게 바꿈)
            _target.CrossFadeColor(pressed ? PressedTint : Color.white, FadeSeconds, true, true);
        }
        #endregion
    }
}
