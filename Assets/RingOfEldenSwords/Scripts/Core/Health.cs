using UnityEngine;
using System;

namespace RingOfEldenSwords.Core
{
    /// <summary>
    /// Health component for characters and entities.
    /// Used by both player and enemies.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 100;

        public Faction faction = Faction.Player;

        private int currentHealth;
        private bool isDead = false;

        public event Action OnDeath;
        public event Action<int> OnDamaged;

        public bool IsDead => isDead;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        void Awake()
        {
            currentHealth = maxHealth;
        }

        public void SetMaxHealth(int value, bool resetCurrent = false)
        {
            maxHealth = value;
            if (resetCurrent) currentHealth = maxHealth;
        }

        public void TakeDamage(int amount)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);
            OnDamaged?.Invoke(amount);

            if (currentHealth <= 0)
                Die();
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            isDead = false;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;
            OnDeath?.Invoke();
        }

        public float GetHealthPercent() => (float)currentHealth / maxHealth;
    }
}
