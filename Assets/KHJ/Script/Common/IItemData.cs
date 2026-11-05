namespace Enzeun.SO
{
    /// <summary>
    /// ID 로 식별되는 아이템 계열 SO 의 공통 인터페이스입니다. (재료, 포션, 월드 오브젝트)
    /// </summary>
    public interface IItemData
    {
        int ID { get; }

#if UNITY_EDITOR
        void SetID(int id);
#endif
    }
}
