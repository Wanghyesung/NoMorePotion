namespace Enzeun.Runtime
{
    /// <summary>
    /// 체력을 회복할 수 있는 대상을 나타내는 인터페이스입니다.
    /// IDamageable 과 함께 구현하면, 상태 효과가 대미지와 회복을 모두 전달할 수 있습니다.
    /// </summary>
    public interface IHealable
    {
        public void Heal(int amount);
    }
}
