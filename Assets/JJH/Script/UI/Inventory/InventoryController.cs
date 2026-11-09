using Cysharp.Threading.Tasks;
using DG.Tweening;
using Enzeun.Runtime;
using Photon.Pun;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

using static JJH_Constants;
using static UnityEngine.InputSystem.InputAction;
using SF = UnityEngine.SerializeField;
public class InventoryController : MonoBehaviour
{
    [Header("전체")]
    [SF] private CanvasGroup invenCanvasGroup;
    [SF] private RectTransform invenRect;
    public InventoryIcon iconPrefab;

    [Header("공용 인벤토리")]
    [SF] private PublicInventory publicInventory;
    [SF] private GameObject publicInven;
    [SF] private ScrollRect publicInventScrollRect;
    [SF] private RectTransform publicInventContentRect;

    [Header("개인 인벤토리")]

    [Header("창고")]
    [SF] private RectTransform storageSideRect;
    

    [Header("시체 상자")]
    [SF] private RectTransform rootBoxSideRect;

    bool flag = false;
    private InputAction closeAction;

    public List<InventoryIcon> publicList;

    public event Action<ItemInstance> OnPublicListAdded;
    public event Action<int> OnPublicListRemoved;
    public event Action<ItemInstance> OnPrivateListAdded;
    public event Action<int> OnPrivateListRemoved;

    private void Awake()
    {
        publicList = new();
    }

    private void Start()
    {
        var state = publicInventory.InvenManager != null ? 
            publicInventory.InvenManager.State : null;
        if (state != null) publicInventory.Bind(state.Materials, state.Potions);
    }

    [Button]
    public void InventoryTest()
    {
        if (flag)
            InventorySideClose();
        else 
            InventorySideOpen();

        flag = !flag;
    }

    private CancellationTokenSource cts;
    private CancellationTokenSource invenToken;

    private void OnEnable()
    {
        closeAction = new InputAction(
            name: "Close",
            binding: "<Keyboard>/tab",
            type: InputActionType.Button
        );

        closeAction.performed += OnTabPressed;

        closeAction.Enable();
    }

    private void OnDisable()
    {
        if (closeAction != null)
        {
            closeAction.Disable();
            closeAction.performed -= OnTabPressed;

            closeAction.Dispose();
            closeAction = null;
        }
    }

    private void OnTabPressed(CallbackContext context)
    {
        invenToken?.Cancel();
        invenToken = new();

        InventoryActive(invenToken.Token).Forget();
    }

    /// <summary>
    /// 닫기 버튼용 이벤트
    /// </summary>
    public void OnTabClosed()
    {
        invenToken?.Cancel();
        invenToken = new();

        InventoryActive(invenToken.Token).Forget();
    }

    /// <summary>
    /// 인벤토리 열기 / 닫기
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async UniTask InventoryActive(CancellationToken token)
    {
        bool fade = false;

        if (invenRect.gameObject.activeSelf)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            fade = false;
        }
        else
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            invenRect.gameObject.SetActive(true);
            fade = true;
        }

        await invenCanvasGroup.DOFade(fade ? 1 : 0, COMMON_FADE_TIME).WithCancellation(token);

        if (!fade)
            invenRect.gameObject.SetActive(false);
    }

    /// <summary>
    /// 인벤토리 사이드 슬롯 열기 호출
    /// </summary>
    public void InventorySideOpen()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();

        InvenSideAnimation(cts.Token, true).Forget();
    }

    /// <summary>
    /// 인벤토리 사이드 슬롯 닫기 호출
    /// </summary>
    public void InventorySideClose()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();

        storageSideRect.gameObject.SetActive(false);
        rootBoxSideRect.gameObject.SetActive(false);

        InvenSideAnimation(cts.Token, false).Forget();
    }

    /// <summary>
    /// 인벤토리 사이드 슬롯 애니메이션 재생 유니태스크 함수
    /// </summary>
    /// <param name="token">취소 토큰</param>
    /// <param name="isAnimation">애니메이션 재생 여부(false는 재생 스킵)</param>
    /// <param name="open">닫기 / 열기 여부</param>
    /// <param name="type">상자 구역인지 창고 구역인지 여부 (false면 상자)</param>
    /// <returns></returns>
    public async UniTask InvenSideAnimation(CancellationToken token, bool isAnimation = true, 
        bool open = false, bool type = false)
    {
        float targetSize = open ? MAX_INVENTORY_SIZE_X : MIN_INVENTORY_SIZE_X;

        if (!open)
        {
            if (storageSideRect.gameObject.activeSelf)
                storageSideRect.gameObject.SetActive(false);

            else if (rootBoxSideRect.gameObject.activeSelf)
                rootBoxSideRect.gameObject.SetActive(false);
        }
        if (type)
        {
            storageSideRect.gameObject.SetActive(true);
            rootBoxSideRect.gameObject.SetActive(false);
        }
        else if (!type)
        {
            rootBoxSideRect.gameObject.SetActive(true);
            storageSideRect.gameObject.SetActive(false);
        }

        if (isAnimation)
            await invenRect.DOSizeDelta(new(targetSize, invenRect.sizeDelta.y), 
                INVEN_SLIDE_TIME).WithCancellation(token);
        else
            invenRect.sizeDelta = new(targetSize, invenRect.sizeDelta.y);
    }

    private void OnDestroy()
    {
        invenToken?.Cancel();
        invenToken?.Dispose();
        cts?.Cancel();
        cts?.Dispose();
    }

    #region 아이템 습득/전환/사용 메서드 , 개인 보류로 인해 이하 private태그를 가진 함수 일시 사용 중단
    public void GetMakePotion(ItemInstance item)
    {
        
    }

    public void GetPublicItem(ItemInstance item)
    {
        OnPublicListAdded?.Invoke(item);
    }

    public void RemovePublicItem(int id)
    {
        OnPublicListRemoved?.Invoke(id);
    }

    public void GetPrivateItem(ItemInstance item)
    {
        OnPrivateListAdded?.Invoke(item);
    }

    public void RemovePrivateItem(int slot)
    {
        OnPrivateListRemoved?.Invoke(slot);
    }
    #endregion
}
