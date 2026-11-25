/// <summary>
/// 현재 세션이 시작될 슬롯과 불러온 데이터 / 씬이 바뀌어도 유지되도록 static
/// 세션 시작 전 타이틀에서 Begin 호출, 슬롯 없으면 저장하지 않음
/// </summary>
public static class SaveSession 
{
    public const int NO_SLOT = -1;

    public static int Slot { get; private set; } = NO_SLOT;

    // 불러온 데이터, 새 게임이면 null
    public static SaveData Loaded { get; private set; }

    
    public static bool HasSlot()
    {
        return Slot != NO_SLOT;
    }

    /// <summary>
    /// 세션 시작 전 호출, 새 게임이면 loaded = null
    /// 타이틀씬에서 고른 슬롯을 게임씬으로까지 가져가야함
    /// </summary>
    public static void Begin(int slot, SaveData loaded)
    {
        Slot = SaveStore.IsValidSlot(slot) ? slot : NO_SLOT;
        Loaded = loaded;
    }

    /// <summary>
    /// 세션 떠날때 호출, 다음판에 이전 슬롯, 데이터 남지 않게
    /// </summary>
    public static void Clear()
    {
        Slot = NO_SLOT;
        Loaded = null;
    }
}
