using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using Enzeun.SO;
using UnityEngine;
using UnityEngine.Events;

public sealed class GuideDamageEvent : GuideEndEvent
{
    [SerializeField] private ParticleSystem attackPrefab;
    [SerializeField, Min(0f)] private float forwardDistance = 1f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0)] private int curseMentalDamage = 100;
    [SerializeField] private MentalStrengthManager mental;
    [Tooltip("개인 포션을 확인하거나 지급한 뒤 순서대로 실행합니다.")]
    [SerializeField] private UnityEvent potionReady = new();

    private CancellationTokenSource attackCancellation;

    // 사망 단계의 Start Event에서 개인 포션이 하나도 없을 때 지급할 포션을 지정한다.
    public void GivePotionIfEmpty(PotionData potion)
    {
        if (!isActiveAndEnabled || attackCancellation != null)
            return;
        attackCancellation = new CancellationTokenSource();
        GivePotionIfEmptyAsync(potion, attackCancellation.Token).Forget();
    }

    private async UniTask GivePotionIfEmptyAsync(PotionData potion, CancellationToken token)
    {
        var inventory = InventoryNetworkSyncManager.Instance;
        int actor = inventory.LocalActor;
        while (!token.IsCancellationRequested)
        {
            var slots = inventory.State.GetPersonal(actor)?.Slots;
            if (slots != null && slots.Any(slot => slot.Kind == InventorySlotKind.Potion))
            {
                potionReady.Invoke();
                return;
            }

            var result = slots != null && slots.All(slot => !slot.IsEmpty)
                ? await inventory.TransferAsync(ContainerRef.Personal(actor, 0), ContainerRef.PackRef)
                : await inventory.GrantPotionAsync(ContainerRef.Personal(actor), potion.potionID);
            if (!result.Success)
                await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
        }
    }

    // 지급이 끝나면 인스펙터에 연결된 공격을 한 번 실행한다.
    public void AttackPlayer()
    {
        if (!isActiveAndEnabled)
            return;

        var player = GameSessionManager.Instance.LocalPlayer.GetComponent<Player>();
        Vector3 forward = player.transform.forward;
        Vector3 origin = player.transform.position + Vector3.up + forward * forwardDistance;
        Instantiate(attackPrefab, origin, Quaternion.LookRotation(-forward));
        player.TakeDamage(new CommonStruct.AttackInfo { Position = origin, Damage = damage });
        if (player.Character.HP > 0)
            mental.DecreaseMentalStrength(curseMentalDamage);
    }

    protected override void OnStop()
    {
        attackCancellation?.Cancel();
        attackCancellation?.Dispose();
        attackCancellation = null;
    }
}
