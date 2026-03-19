using UnityEngine;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Pickups;

namespace RingOfEldenSwords.Enemy
{
    /// <summary>
    /// Attach to an enemy root alongside CharacterOrbitWeapons.
    /// At spawn  : instantiates a hidden OrbitSwordPickup child and stamps it
    ///             with the enemy's current sword count + sword sprite.
    /// At death  : detaches the pickup, scatters it in the world, and activates it.
    ///             This happens inside Health.OnDeath (line 869 of Health.cs),
    ///             BEFORE TDE disables the model or destroys the root, so the
    ///             pickup is safely independent before the enemy disappears.
    /// At respawn: re-instantiates and re-stamps the pickup for the next death.
    /// </summary>
    [AddComponentMenu("RingOfEldenSwords/Enemy/Enemy Orbit Loot")]
    public class EnemyOrbitLoot : TopDownMonoBehaviour
    {
        [Header("Loot Settings")]
        [Tooltip("Prefab with OrbitSwordPickup component to drop on death.")]
        [SerializeField] private GameObject _pickupPrefab;

        [Tooltip("How far from the death position the pickup is scattered (world units).")]
        [SerializeField] private float _scatterRadius = 1.5f;

        // ── Internals ─────────────────────────────────────────────────────────

        private OrbitSwordPickup _pickup;
        private Health _health;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

protected virtual void Awake()
        {
            _health = GetComponent<Health>();
        }

        protected virtual void Start()
        {
            SpawnHiddenPickup();
        }

        protected virtual void OnEnable()
        {
            if (_health != null)
                _health.OnDeath += HandleDeath;
        }

        protected virtual void OnDisable()
        {
            if (_health != null)
                _health.OnDeath -= HandleDeath;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Call this from a CharacterRespawn / respawn handler so the enemy
        /// gets a fresh hidden pickup ready for the next death.
        /// </summary>
        public virtual void OnRespawn()
        {
            SpawnHiddenPickup();
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Instantiates the pickup as a hidden child and stamps it with
        /// the current sword count and sprite.
        /// </summary>
        private void SpawnHiddenPickup()
        {
            if (_pickupPrefab == null)
            {
                Debug.LogWarning($"[EnemyOrbitLoot] No pickup prefab assigned on {gameObject.name}.", this);
                return;
            }

            // Destroy previous pickup if it somehow still exists (edge case: rapid respawn)
            if (_pickup != null)
            {
                Destroy(_pickup.gameObject);
                _pickup = null;
            }

            GameObject go = Instantiate(_pickupPrefab, transform.position, Quaternion.identity, transform);
            _pickup = go.GetComponent<OrbitSwordPickup>();

            if (_pickup == null)
            {
                Debug.LogWarning($"[EnemyOrbitLoot] Pickup prefab on {gameObject.name} has no OrbitSwordPickup component.", this);
                return;
            }

            StampPickup();
        }

        /// <summary>
        /// Reads WeaponCount and sword sprite from CharacterOrbitWeapons,
        /// then calls Init() on the hidden pickup.
        /// </summary>
        private void StampPickup()
        {
            if (_pickup == null) return;

            CharacterOrbitWeapons orbit = GetComponent<CharacterOrbitWeapons>();
            int count = (orbit != null) ? Mathf.Max(1, orbit.WeaponCount) : 1;
            Sprite sprite = GetSwordSprite(orbit);
            _pickup.Init(count, sprite);
        }

        /// <summary>
        /// Reads the sprite from the first active orbiting sword.
        /// Falls back to the WeaponPrefab's SpriteRenderer if no swords are active.
        /// </summary>
        private Sprite GetSwordSprite(CharacterOrbitWeapons orbit)
        {
            if (orbit == null) return null;

            // Try first active sword in orbit ring
            foreach (GameObject sword in orbit.Weapons)
            {
                if (sword == null) continue;
                SpriteRenderer sr = sword.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                    return sr.sprite;
            }

            // Fallback: read from WeaponPrefab directly
            if (orbit.WeaponPrefab != null)
            {
                SpriteRenderer sr = orbit.WeaponPrefab.GetComponent<SpriteRenderer>();
                if (sr != null) return sr.sprite;
            }

            return null;
        }

        /// <summary>
        /// Fires on Health.OnDeath (before TDE disables the model or destroys the root).
        /// Detaches the pickup, positions it with scatter, and activates it.
        /// </summary>
        private void HandleDeath()
        {
            if (_pickup == null) return;

            // Detach from enemy — pickup is now a root scene object
            _pickup.transform.SetParent(null);

            // Random scatter around the death position
            Vector2 scatter = Random.insideUnitCircle.normalized * _scatterRadius;
            _pickup.transform.position = transform.position + new Vector3(scatter.x, scatter.y, 0f);

            // Reveal the pickup
            _pickup.gameObject.SetActive(true);

            // Clear reference — SpawnHiddenPickup will create a fresh one on respawn
            _pickup = null;
        }
    }
}
