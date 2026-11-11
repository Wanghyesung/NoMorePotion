using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using System;
using UnityEngine;

/// <summary>
/// 내 플레이어의 사망 처리. 사망 이벤트를 구독해서 사망 위치에 시체 상자(개인 인벤 → 상자)를 남긴다.
/// GameSessionManager가 만들고, 스폰 직후 Bind / 파괴 시 Dispose 한다.
/// 판이 진행 중인지는 세션이 넘겨준 canHandle 로 판단한다. (이 클래스는 세션을 모름)
/// </summary>
/// <remarks>
/// 사망 이벤트는 IsLocalPlayer 인 클라이언트에서만 발생하므로 자기 플레이어만 구독하면 된다.
/// 상자를 월드에 보이게 하는 건 TeamInventoryState.OnCorpseCreated / OnCorpseRemoved 를 구독하는 쪽의 몫.
/// TODO(부활): 플레이어 쪽 부활 구현 확정 후 여기에 추가 (부활 시 isDead 해제)
/// </remarks>
public sealed class PlayerDeathHandler : IDisposable
{
    private readonly Func<bool> canHandle;
    private Player player;
    // 사망 처리 중인지 (중복 사망 방지). 부활이 생기면 부활 시 해제
    private bool isDead;


    /// <param name="canHandle">사망을 처리해도 되는지 (판이 진행 중인지)</param>
    public PlayerDeathHandler(Func<bool> canHandle)
    {
        this.canHandle = canHandle;
    }

    /// <summary>
    /// 사망 이벤트를 받을 내 플레이어를 연결한다. 스폰 직후 한 번.
    /// </summary>
    public void Bind(Player target)
    {
        Unbind();

        player = target;
        isDead = false;

        // TODO(플레이어): 플레이어 담당이 만든 사망 이벤트 이름으로 연결 (사망 위치 전달)
        //   if (player != null) player.OnDied += OnPlayerDied;
    }

    public void Dispose() => Unbind();

    private void Unbind()
    {
        // TODO(플레이어): Bind 와 짝으로 해제
        //   if (player != null) player.OnDied -= OnPlayerDied;

        player = null;
    }

    /// <summary>
    /// [디버그] 플레이어 이벤트 없이 사망 처리를 실행한다.
    /// </summary>
    public void DebugDie(Vector3 deathPosition) => OnPlayerDied(deathPosition);


    /// <summary>
    /// 내 플레이어가 죽었을 때 (플레이어 사망 이벤트의 구독 함수)
    /// </summary>
    private void OnPlayerDied(Vector3 deathPosition)
    {
        if (canHandle != null && canHandle() == false)
        {
            Debug.Log("[PlayerDeathHandler] 진행중 아님, 사망 무시");
            return;
        }

        if (isDead)
            return;

        isDead = true;
        Debug.Log($"[PlayerDeathHandler] 내 플레이어 사망 : {deathPosition}");

        DropCorpseBoxAsync(deathPosition).Forget();
    }

    /// <summary>
    /// 개인 인벤토리를 사망 위치의 시체 상자로 옮긴다. (마스터가 검증 후 전원에 적용)
    /// </summary>
    private async UniTaskVoid DropCorpseBoxAsync(Vector3 deathPosition)
    {
        InventoryNetworkSyncManager inventory = InventoryNetworkSyncManager.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[PlayerDeathHandler] InventoryNetworkSyncManager 없음, 시체 상자 생략");
            return;
        }

        InventoryResult result = await inventory.DieAsync(deathPosition);

        // 개인 인벤이 비어 있으면 Invalid 로 거부됨 (남길 것이 없음) → 상자 없이 진행
        if (result.Success)
            Debug.Log($"[PlayerDeathHandler] 시체 상자 생성 : ID {result.Outcome.CorpseId}");
        else
            Debug.Log($"[PlayerDeathHandler] 시체 상자 생성 안 됨 : {result.Reason}");
    }
}
