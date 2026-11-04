using UnityEngine;

/*///////////////////////////////////////////
                    Inventory
기능 : 실제 인벤토리 연결 전 지정한 포션 프리팹을 복제하여 반환
 *///////////////////////////////////////////

[System.Serializable]
public sealed class Inventory
{
    [SerializeField] private Potion itemPrefab;

    public Potion GetItem()
    {
        return Object.Instantiate(itemPrefab);
    }
}
