namespace RingOfEldenSwords.Core.Interfaces
{
    public interface IDamageable
    {
        int CurrentHealthInt { get; }
        int MaxHealthInt { get; }
        void TakeDamage(int damage);
        void ResetHealth();
    }
}
