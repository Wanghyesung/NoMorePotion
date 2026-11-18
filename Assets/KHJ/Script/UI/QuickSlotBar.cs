using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;


namespace Enzeun.Runtime
{
    /// <summary>
    /// 인벤토리 밖(HUD)에 보이는 퀵슬롯 바입니다. 내 개인 인벤토리의 칸 0~9(키 1~0)를 읽어서 그리기만 합니다.
    /// 포션을 손에 들면 그 칸이 비므로 "선택 표시"는 없습니다. 빈 칸은 번호만 보입니다.
    /// 인벤토리를 열면(플레이어 조작이 꺼지면) 숨기고, 닫으면 다시 보입니다.
    /// 클릭 같은 조작은 받지 않습니다. (보기 전용, 레이캐스트를 막음)
    ///
    /// 설치 : HUD 캔버스 아래 오브젝트에 붙이고, 칸 아이콘(InventoryIcon) 10개를 순서대로 연결하세요. (CanvasGroup 은 자동으로 붙음)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class QuickSlotBar : MonoBehaviour
    {
        #region Config
        [SerializeField, Required, LabelText("칸 아이콘 (순서 = 칸 번호 0~9)")]
        private List<InventoryIcon> _icons = new List<InventoryIcon>();

        [SerializeField, LabelText("보이고 숨기는 시간(초)"), MinValue(0f)]
        private float _fadeSeconds = 0.15f;
        #endregion

        #region State
        private LocalPersonalInventoryView _view;
        private CanvasGroup _group;
        #endregion

        #region Lifecycle
        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();

            // 보기 전용 : 마우스를 올려도 툴팁이 뜨지 않고 클릭도 받지 않습니다.
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void Start()
        {
            for (int i = 0; i < _icons.Count; i++)
            {
                _icons[i].SetSlotNum(i);
                _icons[i].isPersonalSlot = true;
            }

            _view = new LocalPersonalInventoryView();
            _view.OnRebound += RefreshAll;
            _view.OnSlotChanged += RefreshSlot;
            _view.StartAsync(this.GetCancellationTokenOnDestroy()).Forget();

            RefreshAll();
        }

        private void OnDestroy()
        {
            _view?.Dispose();
            _view = null;
        }
        #endregion

        #region Visibility
        private void Update()
        {
            // 인벤토리를 열면 플레이어 조작이 꺼집니다. (일시정지나 창 포커스를 잃었을 때도 같아서 그때도 숨겨짐)
            InputManager input = InputManager.Instance;
            float target = input == null || input.HasGameplayInput ? 1f : 0f;

            _group.alpha = _fadeSeconds <= 0f
                ? target
                : Mathf.MoveTowards(_group.alpha, target, Time.unscaledDeltaTime / _fadeSeconds);
        }
        #endregion

        #region Refresh
        private void RefreshAll()
        {
            for (int i = 0; i < _icons.Count; i++)
            {
                RefreshSlot(i);
            }
        }

        // 아직 개인 인벤토리가 없으면(아이템을 받기 전) 빈 칸으로 그립니다.
        private void RefreshSlot(int index)
        {
            if (index < 0 || index >= _icons.Count)
            {
                return;
            }

            PersonalInventory inventory = _view != null ? _view.Current : null;
            InventorySlot slot = inventory != null && index < inventory.Capacity ? inventory.Slots[index] : default;

            _icons[index].ShowSlot(slot);
        }
        #endregion
    }
}
