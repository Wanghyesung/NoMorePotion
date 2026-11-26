using System.Collections.Generic;
using Enzeun.Runtime;
using UnityEngine;

public sealed class RecoverCorpsePotionEndEvent : GuideEndEvent
{
    [SerializeField] private RectTransform chestSlots;
    private readonly HashSet<int> droppedPotions = new();
    private PersonalInventory personal;

    // 사망 시점의 위치와 소지한 포션을 회수 안내에 사용한다.
    public void RememberDeathPotions()
    {
        var inventory = InventoryNetworkSyncManager.Instance;
        transform.position = GameSessionManager.Instance.LocalPlayer.GetComponent<Rigidbody>().position;
        droppedPotions.Clear();
        foreach (var slot in inventory.State.GetPersonal(inventory.LocalActor).Slots)
            if (slot.Kind == InventorySlotKind.Potion)
                droppedPotions.Add(slot.Potion.InstanceId);
    }

    // 쓰러지기 전에 소지한 포션이 내 인벤으로 돌아오면 회수 연습을 마친다.
    protected override void OnBegin()
    {
        var inventory = InventoryNetworkSyncManager.Instance;
        personal = inventory.State.GetPersonal(inventory.LocalActor);
        personal.OnSlotChanged += CheckSlot;
        StorageSession.OnCorpseSessionChanged += ShowPotionSlots;
        inventory.State.OnCorpseChanged += OnCorpseChanged;
        ShowPotionSlots(StorageSession.OpenCorpseId);
        for (int i = 0; IsListening && i < personal.Capacity; i++)
            CheckSlot(i);
    }

    private void CheckSlot(int index)
    {
        InventorySlot slot = personal.Slots[index];
        if (slot.Kind == InventorySlotKind.Potion && droppedPotions.Contains(slot.Potion.InstanceId))
            Complete();
    }

    // 열린 상자가 다시 그려질 때 남아 있는 회수 대상만 강조한다.
    private void OnCorpseChanged(int corpseId)
    {
        if (corpseId == StorageSession.OpenCorpseId)
            ShowPotionSlots(corpseId);
    }

    private void ShowPotionSlots(int corpseId)
    {
        foreach (var icon in chestSlots.GetComponentsInChildren<InventoryIcon>(true))
        {
            if (!icon.TryGetComponent(out GuideClickHighlight highlight))
                continue;
            if (corpseId != 0 && icon.gameObject.activeSelf && droppedPotions.Contains(icon.BoundInstanceId))
                highlight.ShowUntilHidden();
            else
                highlight.Hide();
        }
    }

    protected override void OnStop()
    {
        StorageSession.OnCorpseSessionChanged -= ShowPotionSlots;
        var inventory = InventoryNetworkSyncManager.Instance;
        if (inventory != null)
            inventory.State.OnCorpseChanged -= OnCorpseChanged;
        personal.OnSlotChanged -= CheckSlot;
        personal = null;
        if (chestSlots != null)
            ShowPotionSlots(0);
    }
}
