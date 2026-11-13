using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Photon.Pun;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static InputManager;


/*///////////////////////////////////////////
                       Hand
기능 : 포커싱된 손과 손별 보유 포션을 관리하고 소켓에 아이템 연결
 *///////////////////////////////////////////

[DisallowMultipleComponent]
public sealed class Hand : MonoBehaviour
{
    public enum Side { Left = 0, Right = 1 }

    #region 손과 소켓
    [SerializeField] private Side focusedSide;
    [SerializeField] private Transform leftSocket;
    [SerializeField] private Transform rightSocket;

    private PotionMover leftPotion;
    private PotionMover rightPotion;
    #endregion

    #region 포션 생성과 사용
    [SerializeField] private Potion itemPrefab;
    [SerializeField] private UnityEvent<PotionMover> OnDrink = new UnityEvent<PotionMover>();
    private bool isTakingOut;
    #endregion

    #region 플레이어와 입력 상태
    private Player player;
    private PhotonView playerView;
    private bool started;
    private bool inputSubscribed;
    #endregion

    public Side FocusedSide => focusedSide;
    public void Focus(Side side)
    {
        if (focusedSide == side)
            return;

        focusedSide = side;
        OnFocusChanged?.Invoke((int)focusedSide);
    }

    /// <summary>
    /// 손 포커스가 실제로 변경된 뒤 알림. 왼손은 0, 오른손은 1을 전달합니다.
    /// </summary>
    public event Action<int> OnFocusChanged;
    public event Action OnPotionHeld;

    /// <summary>
    /// 흔들기 시작 시 알림. 첫 번째 인자는 왼손, 두 번째 인자는 오른손의 포션입니다.
    /// 포션이 없는 손은 null로 전달합니다.
    /// </summary>
    public event Action<PotionInstance, PotionInstance> OnShake;

    /// <summary>
    /// 흔들기 상태 종료 시 알림. 정상 완료와 다른 상태로 인한 중단을 포함합니다.
    /// </summary>
    public event Action OnShakeFinished;

    public PotionMover FocusedPotion => GetPotion(focusedSide);
    public PotionMover OtherPotion => GetPotion(focusedSide == Side.Left ? Side.Right : Side.Left);
    public bool IsBothHandsEmpty => leftPotion == null && rightPotion == null;
    public Side TakeSide => FocusedPotion == null
        ? focusedSide : (focusedSide == Side.Left ? Side.Right : Side.Left);

    private void Awake()
    {
        player = GetComponent<Player>();
        playerView = GetComponent<PhotonView>();

        // 같은 수신 묶음의 포션 생성 이벤트가 Start보다 먼저 올 수 있습니다. -> TestCode
        if (itemPrefab != null && PhotonNetwork.PrefabPool is DefaultPool pool)
            pool.ResourceCache[itemPrefab.name] = itemPrefab.gameObject;
    }

    private void Start()
    {
        started = true;
        SubscribeInput();
    }

    private void OnEnable()
    {
        if (started == true)
            SubscribeInput();
    }

    private void SubscribeInput()
    {
        if (player.IsLocalPlayer == false || inputSubscribed == true)
            return;
        Instance.SwitchHandAction.performed += SwitchFocus;
        inputSubscribed = true;
    }

    private void OnDisable()
    {
        if (inputSubscribed == true && Instance != null)
            Instance.SwitchHandAction.performed -= SwitchFocus;
        inputSubscribed = false;
    }

    private void SwitchFocus(InputAction.CallbackContext context) => SwitchFocus();

    public void SwitchFocus() => Focus(focusedSide == Side.Left ? Side.Right : Side.Left);
    public bool Hold(Side side, PotionMover potion)
    {
        if (GetPotion(side) != null)
            return false;

        potion.Hold(side == Side.Left ? leftSocket : rightSocket);
        SetPotion(side, potion);
        OnPotionHeld?.Invoke();
        return true;
    }

    public int EmptyHandCount => (leftPotion == null ? 1 : 0) + (rightPotion == null ? 1 : 0);

    public void Shake()
    {
        PotionInstance left = leftPotion != null ? leftPotion.PotionInstance : null;
        PotionInstance right = rightPotion != null ? rightPotion.PotionInstance : null;
        OnShake?.Invoke(left, right);
    }

    public void FinishShake()
    {
        Debug.Log("흔들기 끝남", this);
        OnShakeFinished?.Invoke();
    }

    /// <summary>
    /// 인벤토리에서 포션을 무작위로 꺼내 빈 손에 듭니다. (공용 인벤 우선, 없으면 개인 인벤, 없으면 아무 일도 없음)
    /// 빈 손의 수만큼만 꺼내고, 요청을 보냈으면 true 입니다. 결과는 비동기로 손에 반영됩니다.
    /// </summary>
    public bool TakeOutPotion()
    {
        int emptyCount = EmptyHandCount;
        if (emptyCount == 0 || isTakingOut == true)
            return false;

        InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
        if (manager == null)
            return false;

        TakeOutAsync(() => manager.TakeOutRandomAsync(emptyCount)).Forget();
        return true;
    }

    /// <summary>
    /// 개인 인벤토리 퀵슬롯(0~9, 키 1~0)의 포션을 정확히 꺼내 빈 손에 듭니다.
    /// </summary>
    public bool TakeOutQuickSlot(int slot)
    {
        if (EmptyHandCount == 0 || isTakingOut == true)
            return false;

        InventoryNetworkSyncManager manager = InventoryNetworkSyncManager.Instance;
        if (manager == null)
            return false;

        TakeOutAsync(() => manager.TakeOutQuickSlotAsync(slot)).Forget();
        return true;
    }

    private async UniTaskVoid TakeOutAsync(Func<UniTask<InventoryResult>> request)
    {
        isTakingOut = true;

        try
        {
            InventoryResult result = await request();

            // 기다리는 사이 이 오브젝트가 사라졌으면 더 할 것이 없습니다.
            if (this == null || result.Success == false || result.Outcome.Potions == null)
                return;

            foreach (PotionInstance instance in result.Outcome.Potions)
            {
                // 이미 인벤토리에서 빠진 포션이라 들 손이 없으면 사라집니다. (요청 중에는 새로 꺼낼 수 없어 보통 생기지 않음)
                if (TryGetEmptySide(out Side side) == false)
                {
                    Debug.LogWarning("[Hand] : 포션을 들 빈 손이 없어 꺼낸 포션이 사라졌습니다.");
                    break;
                }

                GameObject potionObj = PhotonNetwork.Instantiate(itemPrefab.name, transform.position, Quaternion.identity);
                Potion potion = potionObj.GetComponent<Potion>();

                potion.Initialize(instance);
                Hold(side, potion.potionMover);

                playerView.RPC(nameof(RpcTakeOutPotion), RpcTarget.Others,
                    potionObj.GetComponent<PhotonView>().ViewID, (int)side, instance.Data.potionID);
            }
        }
        finally
        {
            isTakingOut = false;
        }
    }

    // 포커스한 손이 비어 있으면 그 손, 아니면 반대 손을 돌려줍니다. 둘 다 차 있으면 false 입니다.
    private bool TryGetEmptySide(out Side side)
    {
        Side other = focusedSide == Side.Left ? Side.Right : Side.Left;

        if (GetPotion(focusedSide) == null)
        {
            side = focusedSide;
            return true;
        }

        side = other;
        return GetPotion(other) == null;
    }

    public void Throw(Side side, ThrowData throwData)
    {
        if (player.IsLocalPlayer == false)
            return;

        PotionMover potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        potion.transform.SetParent(null, true);
        potion.Throw(throwData);

        // 다른 클라이언트도 같은 목표점과 높이로 포물선을 복원한다.
        playerView.RPC(nameof(RpcThrowPotion), RpcTarget.Others,
            potion.GetComponent<PhotonView>().ViewID, (int)side,
            throwData.Origin, throwData.Target, throwData.Gravity, throwData.ArcHeight);
    }


    public void Drink(Side side)
    {
        PotionMover potion = GetPotion(side);
        if (potion == null)
            return;

        SetPotion(side, null);
        OnDrink.Invoke(potion);
        potion.Drink(transform.position);
    }

    public void Drink() => Drink(focusedSide);

    public PotionMover GetPotion(Side side) => side == Side.Left ? leftPotion : rightPotion;
    private void SetPotion(Side side, PotionMover potion)
    {
        if (side == Side.Left)
            leftPotion = potion;
        else
            rightPotion = potion;
    }


    /* ------------------------------------------------
                        네트워크 이벤트
    ---------------------------------------------------*/
    [PunRPC]
    private void RpcTakeOutPotion(int potionViewId, int sideValue, int potionId, PhotonMessageInfo info)
    {
        if (info.Sender != playerView.Owner || playerView.IsMine
            || (sideValue != (int)Side.Left && sideValue != (int)Side.Right))
            return;

        PhotonView potionView = PhotonView.Find(potionViewId);
        if (potionView == null || potionView.Owner != info.Sender)
            return;

        Side side = (Side)sideValue;
        if (GetPotion(side) != null
            || PotionDataManager.Instance.TryGetPotionData(potionId, out var data) == false)
            return;

        Potion potion = potionView.GetComponent<Potion>();
        potion.InitializeVisual(data);
        Hold(side, potion.potionMover);
    }


    [PunRPC]
    private void RpcThrowPotion(int potionViewId, int sideValue, Vector3 origin, Vector3 target,
        Vector3 gravity, float arcHeight, PhotonMessageInfo info)
    {
        if (info.Sender != playerView.Owner || playerView.IsMine
            || (sideValue != (int)Side.Left && sideValue != (int)Side.Right))
            return;

        PhotonView potionView = PhotonView.Find(potionViewId);
        if (potionView == null || potionView.Owner != info.Sender)
            return;

        PotionMover potion = potionView.GetComponent<PotionMover>();
        Side side = (Side)sideValue;
        if (GetPotion(side) != potion)
            return;

        if (arcHeight <= 0f || gravity.sqrMagnitude <= 0f)
            return;
        var data = new ThrowData(origin, target, gravity, arcHeight);

        SetPotion(side, null);
        potion.transform.SetParent(null, true);
        // 송신 후 지난 시간만큼 이동을 보정하며, 원격 포션은 이동 연출만 담당한다.
        potion.ThrowVisual(data, Mathf.Max(0f, (float)(PhotonNetwork.Time - info.SentServerTime)));
    }

    
}
