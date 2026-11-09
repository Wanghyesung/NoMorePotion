using Enzeun.Runtime;
using System;
using UnityEngine;

using SF = UnityEngine.SerializeField;

[Serializable]
public class ItemInstance
{
    [SF] private int count = 1;

    public InventorySlotKind kind;

    public int itemId;
    // 포션 Id, 재료는 0
    public int instanceId;

    // 아이템 아이콘
    public Sprite itemImage;
    // int read용 아이템 개수
    public int ItemCount { 
        get => count; 
        set {
            if (count == value) return;
            count = value;
            OnChanged?.Invoke();
        } 
    }

    public event Action OnChanged;

    public ItemInstance(int id, Sprite sprite, InventorySlotKind kind, int count = 1, int instanceId = 0)
    {
        itemId = id;
        itemImage = sprite;
        this.kind = kind;
        this.count = count;
        this.instanceId = instanceId;
    }
}
