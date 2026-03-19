using UnityEngine;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Pickups;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.Enemy
{
    /// <summary>
    /// Attach to an enemy root alongside CharacterOrbitWeapons.
    /// At spawn  : instantiates a hidden OrbitWeaponPickup child (not yet stamped).
    /// At death  : stamps the pickup with the live WeaponDefinition + count,
    ///             detaches it, scatters it, and activates it.
    ///             Stamping at death (not at spawn) guarantees CharacterOrbitWeapons
    ///             has already run Initialization() and WeaponDefinition is valid.
    /// At respawn: re-instantiates a fresh hidden pickup for the next death.
    /// </summary>
    [AddComponentMenu("RingOfEldenSwords/Enemy/Enemy Orbit Loot")]
    public class EnemyOrbitLoot : TopDownMonoBehaviour
    {
        [Header("Loot Settings")]
        [Tooltip("Prefab with OrbitWeaponPickup component to drop on death.")]
        [SerializeField] private GameObject _pickupPrefab;

        [Tooltip("How far from the death position the pickup is scattered (world units).")]
        [SerializeField] private float _scatterRadius = 1.5f;

        // ── Internals ─────────────────────────────────────────────────────────

        private OrbitWeaponPickup _pickup;
        private Health            _health;

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
        /// Call from a respawn handler so the enemy gets a fresh pickup for the next death.
        /// </summary>
        public virtual void OnRespawn()
        {
            SpawnHiddenPickup();
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Instantiates the pickup as an inactive child — not yet stamped.
        /// Stamping is deferred to HandleDeath() so it reads data after
        /// CharacterOrbitWeapons.Initialization() has completed.
        /// </summary>
        private void SpawnHiddenPickup()
        {
            if (_pickupPrefab == null)
            {
                Debug.LogWarning($"[EnemyOrbitLoot] No pickup prefab assigned on {gameObject.name}.", this);
                return;
            }

            // Destroy stale pickup from a previous life (rapid-respawn edge case)
            if (_pickup != null)
            {
                Destroy(_pickup.gameObject);
                _pickup = null;
            }

            GameObject go = Instantiate(_pickupPrefab, transform.position, Quaternion.identity, transform);
            _pickup = go.GetComponent<OrbitWeaponPickup>();

            if (_pickup == null)
            {
                Debug.LogWarning($"[EnemyOrbitLoot] Pickup prefab on {gameObject.name} has no OrbitWeaponPickup component.", this);
                Destroy(go);
            }
            // Pickup stays inactive until HandleDeath activates it
        }

        /// <summary>
        /// Reads WeaponDefinition and WeaponCount from CharacterOrbitWeapons
        /// and calls Init() on the pickup.
        /// Called at death time — after Initialization() has run — so the
        /// definition reference is guaranteed to be valid.
        /// </summary>
        private void StampPickup()
        {
            if (_pickup == null) return;

            CharacterOrbitWeapons orbit = GetComponent<CharacterOrbitWeapons>();
            int                    count = (orbit != null) ? Mathf.Max(1, orbit.WeaponCount) : 1;
            OrbitWeaponDefinition  def   = orbit != null ? orbit.WeaponDefinition : null;

            Debug.Log($"[EnemyOrbitLoot] StampPickup — orbit={orbit != null} " +
                      $"WeaponDefinition={def?.WeaponName ?? "NULL"} " +
                      $"WeaponCount={count} on {gameObject.name}");

            _pickup.Init(count, def);
        }

        /// <summary>
        /// Fires on Health.OnDeath — before TDE disables the model.
        /// Stamps, detaches, scatters, and activates the pickup.
        /// </summary>
        private void HandleDeath()
        {
            if (_pickup == null) return;

            // Stamp now — CharacterOrbitWeapons.Initialization() has already run
            StampPickup();

            // Detach from enemy so it survives the enemy being disabled/destroyed
            _pickup.transform.SetParent(null);

            // Random scatter around the death position
            Vector2 scatter = Random.insideUnitCircle.normalized * _scatterRadius;
            _pickup.transform.position = transform.position + new Vector3(scatter.x, scatter.y, 0f);

            // Reveal the pickup
            _pickup.gameObject.SetActive(true);

            // Clear reference — SpawnHiddenPickup creates a fresh one on respawn
            _pickup = null;
        }
    }
}
