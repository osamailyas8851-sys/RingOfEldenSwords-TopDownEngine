using UnityEngine;
using System;
using System.Collections;
using MoreMountains.TopDownEngine;

namespace RingOfEldenSwords.Combat.Weapons
{
    /// <summary>
    /// Handles combat for an orbiting sword: clashing with enemy swords and
    /// damaging characters. Faction is determined purely by GameObject tag
    /// ("Player" or "Enemy") — set by CharacterOrbitWeapons at spawn time,
    /// matching the TDE convention used everywhere else in the engine.
    /// </summary>
    public class OrbitSwordCombat : MonoBehaviour
    {
        public enum SwordState { Active, Destroyed }

        [Header("Combat Stats")]
        public float MaxHealth = 10f;
        public float ClashDamage = 10f;
        public int EntityDamage = 10;

        [Header("Cooldowns")]
        public float ClashCooldown = 0.1f;
        public float EntityHitCooldown = 0.5f;

        public event Action<GameObject> OnDestroyed;
        public event Action<OrbitSwordCombat> OnClash;

        public float CurrentHealth => _currentHealth;
        public bool IsAlive => _currentHealth > 0f && _state == SwordState.Active;

        private SpriteRenderer _spriteRenderer;
        private float _currentHealth;
        private SwordState _state = SwordState.Active;
        private bool _canClash = true;
        private bool _canHitEntity = true;
        private Coroutine _clashCooldown;
        private Coroutine _entityCooldown;
        private WaitForSeconds _waitClash;
        private WaitForSeconds _waitEntity;

        void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _waitClash  = new WaitForSeconds(ClashCooldown);
            _waitEntity = new WaitForSeconds(EntityHitCooldown);
            ResetHealth();
        }

        void OnEnable()
        {
            ResetHealth();
            _state        = SwordState.Active;
            _canClash     = true;
            _canHitEntity = true;
            if (_clashCooldown  != null) { StopCoroutine(_clashCooldown);  _clashCooldown  = null; }
            if (_entityCooldown != null) { StopCoroutine(_entityCooldown); _entityCooldown = null; }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsAlive) return;
            if (other.gameObject == gameObject) return;
            if (other.transform.IsChildOf(transform)) return;

            // Clash check: other collider belongs to an enemy sword (different tag)
            OrbitSwordCombat otherSword = other.GetComponentInParent<OrbitSwordCombat>();
            if (_canClash && otherSword != null && otherSword != this
                && !otherSword.gameObject.CompareTag(gameObject.tag) && otherSword.IsAlive)
            {
                PerformClash(otherSword);
                return;
            }

            // Entity damage check: hit a character with a Health component
            if (!_canHitEntity) return;
            Health tdeHealth = other.GetComponentInParent<Health>();
            if (tdeHealth != null && CanDamageCharacter(tdeHealth))
                DamageCharacter(tdeHealth);
        }

        public void ResetHealth()
        {
            _currentHealth = MaxHealth;
            _state         = SwordState.Active;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            if (_currentHealth <= 0f) DestroySword();
        }

        public void SetSprite(Sprite sprite)
        {
            if (_spriteRenderer != null && sprite != null)
                _spriteRenderer.sprite = sprite;
        }

        public Sprite GetSprite() => _spriteRenderer != null ? _spriteRenderer.sprite : null;

        public void SetStats(float maxHealth = -1f, float clashDamage = -1f,
                             int entityDamage = -1, float clashCooldown = -1f,
                             float entityHitCooldown = -1f)
        {
            if (maxHealth    >= 0f) MaxHealth    = maxHealth;
            if (clashDamage  >= 0f) ClashDamage  = clashDamage;
            if (entityDamage >= 0)  EntityDamage = entityDamage;
            if (clashCooldown >= 0f)
            {
                ClashCooldown = clashCooldown;
                _waitClash    = new WaitForSeconds(clashCooldown);
            }
            if (entityHitCooldown >= 0f)
            {
                EntityHitCooldown = entityHitCooldown;
                _waitEntity       = new WaitForSeconds(entityHitCooldown);
            }
        }

        private void PerformClash(OrbitSwordCombat other)
        {
            OnClash?.Invoke(other);
            other.TakeDamage(ClashDamage);
            TakeDamage(other.ClashDamage);
            // Start cooldown on both swords so neither can re-clash immediately
            if (IsAlive && _clashCooldown == null)
                _clashCooldown = StartCoroutine(ClashCooldownRoutine());
            if (other.IsAlive && other._clashCooldown == null)
                other._clashCooldown = other.StartCoroutine(other.ClashCooldownRoutine());
        }

        private IEnumerator ClashCooldownRoutine()
        {
            _canClash = false;
            yield return _waitClash;
            _canClash      = true;
            _clashCooldown = null;
        }

        /// <summary>
        /// A player sword only damages enemies and vice versa — matches the
        /// tag convention used by TDE's DamageOnTouch and Health systems.
        /// </summary>
        private bool CanDamageCharacter(Health tdeHealth)
        {
            if (tdeHealth.CurrentHealth <= 0f) return false;
            // Player sword damages enemies; enemy sword damages players
            if (gameObject.CompareTag("Player") && !tdeHealth.gameObject.CompareTag("Enemy"))  return false;
            if (gameObject.CompareTag("Enemy")  && !tdeHealth.gameObject.CompareTag("Player")) return false;
            return true;
        }

        private void DamageCharacter(Health tdeHealth)
        {
            tdeHealth.Damage(EntityDamage, gameObject, 0.1f, 0.5f, Vector3.zero);
            if (!isActiveAndEnabled) return;
            _entityCooldown = StartCoroutine(EntityCooldownRoutine());
        }

        private IEnumerator EntityCooldownRoutine()
        {
            _canHitEntity = false;
            yield return _waitEntity;
            _canHitEntity   = true;
            _entityCooldown = null;
        }

        private void DestroySword()
        {
            _state = SwordState.Destroyed;
            // Stop cooldown coroutines before destroy so they don't linger
            if (_clashCooldown  != null) { StopCoroutine(_clashCooldown);  _clashCooldown  = null; }
            if (_entityCooldown != null) { StopCoroutine(_entityCooldown); _entityCooldown = null; }
            OnDestroyed?.Invoke(gameObject);
            Destroy(gameObject);
        }
    }
}
