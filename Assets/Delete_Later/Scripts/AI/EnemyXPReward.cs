using UnityEngine;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Add to any enemy prefab alongside Health.
    /// When the enemy dies, broadcasts an XPGainEvent so the player's PlayerXP picks it up.
    /// Follows the same OnDeath hook pattern as EnemyOrbitLoot.
    /// </summary>
    [AddComponentMenu("TopDown Engine/Character/XP/Enemy XP Reward")]
    public class EnemyXPReward : TopDownMonoBehaviour
    {
        [Header("XP Settings")]
        [Tooltip("Amount of XP the player gains when this enemy is killed.")]
        [SerializeField] protected int _xpReward = 25;

        /// <summary>
        /// The XP reward value, readable from outside if needed.
        /// </summary>
        public int XPReward => _xpReward;

        protected Health _health;

        protected virtual void Awake()
        {
            _health = GetComponent<Health>();

            if (_health == null)
                Debug.LogWarning($"[EnemyXPReward] No Health component found on {gameObject.name}. " +
                                 "XP reward will not fire on death.", this);
        }

        protected virtual void OnEnable()
        {
            if (_health != null) _health.OnDeath += HandleDeath;
        }

        protected virtual void OnDisable()
        {
            if (_health != null) _health.OnDeath -= HandleDeath;
        }

        protected virtual void HandleDeath()
        {
            XPGainEvent.Trigger(_xpReward);
        }
    }
}
