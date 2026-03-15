using UnityEngine;
using System;
using System.Collections;
using RingOfEldenSwords.Core;
using RingOfEldenSwords.Core.Interfaces;

namespace RingOfEldenSwords.Combat.Weapons
{
    public class WeaponBehaviour : MonoBehaviour, IDamageable
    {
        public enum WeaponState { Active, Breaking, Destroyed }

        [Header("Faction")]
        public Faction ownerFaction = Faction.Player;

        [Header("Base Stats")]
        [SerializeField] private float baseMaxHealth = 10f;
        [SerializeField] private float baseClashDamage = 10f;
        [SerializeField] private int baseEntityDamage = 10;

        [Header("Collision Settings")]
        [SerializeField] private float hitCooldown = 0.1f;
        [SerializeField] private float entityHitCooldown = 0.5f;

        [Header("Animation (Optional)")]
        [SerializeField] private Animator animator;
        [SerializeField] private float destroyDelay = 0f;

        private float currentHealth;
        private WeaponState currentState = WeaponState.Active;
        private float upgradeMaxHealth;
        private float upgradeClashDamage;
        private int upgradeEntityDamage;
        private bool hasUpgrade = false;
        private bool canDamage = true;
        private bool canDamageEntity = true;
        private Coroutine cooldownRoutine;
        private Coroutine entityCooldownRoutine;
        private Coroutine destroyRoutine;
        private WaitForSeconds waitHitCooldown;
        private WaitForSeconds waitEntityCooldown;

        private static System.Collections.Generic.Dictionary<RingOfEldenSwords.Core.Health, RingOfEldenSwords.Combat.Orbit.OrbitSystem>
            orbitCache = new System.Collections.Generic.Dictionary<RingOfEldenSwords.Core.Health, RingOfEldenSwords.Combat.Orbit.OrbitSystem>();

        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int BreakTrigger = Animator.StringToHash("Break");
        private const bool DebugLogs = false;

        public event Action<float> OnDamageTaken;
        public event Action OnBreakStart;
        public event Action<GameObject> OnDestroyed;
        public event Action<WeaponBehaviour> OnClash;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => hasUpgrade ? upgradeMaxHealth : baseMaxHealth;
        public float ClashDamage => hasUpgrade ? upgradeClashDamage : baseClashDamage;
        public int EntityDamage => hasUpgrade ? upgradeEntityDamage : baseEntityDamage;
        public bool IsAlive => currentHealth > 0 && currentState == WeaponState.Active;
        public WeaponState State => currentState;
        int IDamageable.CurrentHealthInt => Mathf.RoundToInt(currentHealth);
        int IDamageable.MaxHealthInt => Mathf.RoundToInt(MaxHealth);

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            waitHitCooldown = new WaitForSeconds(hitCooldown);
            waitEntityCooldown = new WaitForSeconds(entityHitCooldown);
            ResetHealth();
        }

        void OnEnable()
        {
            ResetHealth();
            currentState = WeaponState.Active;
            canDamage = true;
            canDamageEntity = true;
            if (cooldownRoutine != null) { StopCoroutine(cooldownRoutine); cooldownRoutine = null; }
            if (entityCooldownRoutine != null) { StopCoroutine(entityCooldownRoutine); entityCooldownRoutine = null; }
            if (destroyRoutine != null) { StopCoroutine(destroyRoutine); destroyRoutine = null; }
        }

        void OnDestroy()
        {
            orbitCache.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleHitboxTrigger(other);
        }

        public void HandleHitboxTrigger(Collider2D other)
        {
            if (!IsAlive) return;
            if (other.gameObject == gameObject) return;
            if (other.transform.IsChildOf(transform)) return;

            // Sword vs sword clash
            WeaponBehaviour otherWeapon = other.GetComponentInParent<WeaponBehaviour>();
            if (canDamage && otherWeapon != null && otherWeapon != this)
            {
                if (CanClash(otherWeapon))
                {
                    PerformClash(otherWeapon);
                    return;
                }
            }

            if (!canDamageEntity) return;

            // Sword vs TDE character — fully qualified to avoid ambiguity with Core.Health
            MoreMountains.TopDownEngine.Health tdeHealth =
                other.GetComponentInParent<MoreMountains.TopDownEngine.Health>();
            if (tdeHealth != null && CanDamageTDEEntity(tdeHealth))
                DamageTDEEntity(tdeHealth);
        }

        private bool CanClash(WeaponBehaviour other)
        {
            return other.ownerFaction != ownerFaction && other.IsAlive && IsAlive;
        }

        private void PerformClash(WeaponBehaviour other)
        {
            OnClash?.Invoke(other);
            other.TakeDamage(ClashDamage);
            TakeDamage(other.ClashDamage);
            cooldownRoutine = StartCoroutine(ClashCooldownRoutine());
        }

        private bool CanDamageTDEEntity(MoreMountains.TopDownEngine.Health tdeHealth)
        {
            if (tdeHealth.CurrentHealth <= 0) return false;

            bool isPlayerWeapon = ownerFaction == Faction.Player;
            bool targetIsEnemy  = tdeHealth.gameObject.CompareTag("Enemy");
            bool targetIsPlayer = tdeHealth.gameObject.CompareTag("Player");

            if (isPlayerWeapon && !targetIsEnemy) return false;
            if (!isPlayerWeapon && !targetIsPlayer) return false;

            return true;
        }

        private void DamageTDEEntity(MoreMountains.TopDownEngine.Health tdeHealth)
        {
            tdeHealth.Damage(EntityDamage, gameObject, 0.1f, 0.5f, Vector3.zero);
            entityCooldownRoutine = StartCoroutine(EntityCooldownRoutine());
            if (DebugLogs)
                Debug.Log($"[WeaponBehaviour] {ownerFaction} sword hit {tdeHealth.gameObject.name} for {EntityDamage}");
        }

        private IEnumerator ClashCooldownRoutine()
        {
            canDamage = false;
            yield return waitHitCooldown;
            canDamage = true;
            cooldownRoutine = null;
        }

        private IEnumerator EntityCooldownRoutine()
        {
            canDamageEntity = false;
            yield return waitEntityCooldown;
            canDamageEntity = true;
            entityCooldownRoutine = null;
        }

        public void ResetHealth()
        {
            currentHealth = MaxHealth;
            currentState = WeaponState.Active;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            OnDamageTaken?.Invoke(amount);
            TriggerAnimation(HitTrigger);
            if (currentHealth <= 0) Die();
        }

        void IDamageable.TakeDamage(int damage) => TakeDamage((float)damage);
        void IDamageable.ResetHealth() => ResetHealth();
        public float GetHealthPercent() => currentHealth / MaxHealth;

        private void Die()
        {
            currentState = WeaponState.Breaking;
            OnBreakStart?.Invoke();
            TriggerAnimation(BreakTrigger);
            if (destroyDelay > 0)
                destroyRoutine = StartCoroutine(DelayedDestroyRoutine());
            else
                DestroyWeapon();
        }

        private IEnumerator DelayedDestroyRoutine()
        {
            yield return new WaitForSeconds(destroyDelay);
            DestroyWeapon();
        }

        private void DestroyWeapon()
        {
            currentState = WeaponState.Destroyed;
            OnDestroyed?.Invoke(gameObject);
            orbitCache.Clear();
            Destroy(gameObject);
        }

        public void SetUpgradeStats(float maxHealth, float clashDamage, int entityDamage)
        {
            float healthPercent = currentHealth / MaxHealth;
            upgradeMaxHealth = maxHealth;
            upgradeClashDamage = clashDamage;
            upgradeEntityDamage = entityDamage;
            hasUpgrade = true;
            currentHealth = MaxHealth * healthPercent;
        }

        private void TriggerAnimation(int triggerHash)
        {
            if (animator != null && animator.isActiveAndEnabled)
                animator.SetTrigger(triggerHash);
        }

        public void OnBreakAnimationComplete() => DestroyWeapon();
    }
}
