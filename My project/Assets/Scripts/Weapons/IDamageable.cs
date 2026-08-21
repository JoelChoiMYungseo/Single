namespace VRGame.Combat
{
    /// <summary>피격 가능한 대상이 구현하는 최소 인터페이스. 총기 등 데미지 소스가 대상 타입을 몰라도 되게 분리한다.</summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}
