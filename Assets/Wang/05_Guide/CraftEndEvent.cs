using Enzeun.Runtime;
using UnityEngine;

public sealed class CraftEndEvent : GuideEndEvent
{
    [SerializeField] private CombinationController combinationController;
    [Tooltip("0이면 종류에 관계없이 제작 성공을 인정합니다.")]
    [SerializeField] private int requiredPotionId;
    [Tooltip("켜면 함정 포션 제작은 완료로 인정하지 않습니다.")]
    [SerializeField] private bool requireNormalPotion;
    private CombinationController subscribedController;

    protected override void OnBegin()
    {
        subscribedController = combinationController;
        //subscribedController.PotionCrafted += OnPotionCrafted;
    }

    protected override void OnStop()
    {
        //if (subscribedController != null)
            //subscribedController.PotionCrafted -= OnPotionCrafted;
        //subscribedController = null;
    }

    private void OnPotionCrafted(PotionInstance potion)
    {
        if (requiredPotionId != 0 && potion.Data.potionID != requiredPotionId)
            return;
        if (requireNormalPotion && potion.TrapKeyword != null)
            return;
        Complete();
    }
}
