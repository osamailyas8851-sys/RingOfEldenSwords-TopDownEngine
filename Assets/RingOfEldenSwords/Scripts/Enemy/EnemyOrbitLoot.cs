using UnityEngine;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Pickups;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.Enemy
{
    /// <summary>
    /// Attach to an enemy root alongside CharacterOrbitWeapons.
    /// Requires an OrbitWeaponPickup to be embedded as an inactive child
    /// of the enemy prefab in the Editor — no Instantiate() ever occurs.
    ///
    /// At scene load : finds the child pickup, stamps weapon data once (Start).
    /// At death      : scatters and activates the already-stamped pickup.
    /// At respawn    : deactivates the pickup — ready for next death cycle.
    /// </summary>
    [AddComponentMenu("RingOfEldenSwords/Enemy/Enemy Orbit Loot")]
    public class EnemyOrbitLoot : TopDownMonoBehaviour
    {
        [Header("Loot Settings")]
        [Tooltip("How far from the death position the pickup is scattered (world units).")]
        [SerializeField] private float _scatterRadius = 1.5f;

        // ── Internals ─────────────────────────────────────────────────────────

        private OrbitWeaponPickup _pickup;
        private Health            _health;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _health = GetComponent<Health>();

            // Find the embedded pickup child — includeInactive=true is required
            // because the pickup starts inactive in the prefab.
            _pickup = GetComponentInChildren<OrbitWeaponPickup>(true);

            if (_pickup == null)
            {
                Debug.LogWarning($"[EnemyOrbitLoot] No OrbitWeaponPickup found as child of {gameObject.name}. " +
                                 "Add OrbitWeaponPickup as an inactive child in the prefab.", this);
                return;
            }

            // Force PickableItem.Start() to run NOW at scene load, not at first
            // SetActive(true) on death. This pre-warms MMFeedbacks.Initialization()
            // so there is zero cost when the pickup is revealed at death time.
            _pickup.gameObject.SetActive(true);
            _pickup.gameObject.SetActive(false);
        }

        protected virtual void Start()
        {
            if (_pickup == null)
            {
                Debug.LogWarning($"[EnemyOrbitLoot] Start: _pickup is null on {gameObject.name}");
                return;
            }

            CharacterOrbitWeapons orbit = GetComponent<CharacterOrbitWeapons>();
            Debug.Log($"[EnemyOrbitLoot] Start: orbit={orbit != null} def={orbit?.WeaponDefinition?.WeaponName ?? "NULL"} count={orbit?.WeaponCount}");

            // Stamp weapon data once at spawn — CharacterOrbitWeapons.Initialization()
            // has run by now so WeaponDefinition and WeaponCount are valid.
            StampPickup();
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
        /// Call from a respawn handler to hide the pickup — ready for next death cycle.
        /// </summary>
        public virtual void OnRespawn()
        {
            if (_pickup == null) return;
            _pickup.gameObject.SetActive(false);
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Reads WeaponDefinition and WeaponCount from CharacterOrbitWeapons
        /// and stamps them onto the pickup. Called at death time — after
        /// CharacterOrbitWeapons.Initialization() has run — so data is valid.
        /// </summary>
        private void StampPickup()
        {
            CharacterOrbitWeapons orbit = GetComponent<CharacterOrbitWeapons>();
            int                   count = (orbit != null) ? Mathf.Max(1, orbit.WeaponCount) : 1;
            OrbitWeaponDefinition def   = orbit != null ? orbit.WeaponDefinition : null;
            _pickup.Init(count, def);
        }

        /// <summary>
        /// Fires on Health.OnDeath. Scatters and activates the embedded pickup.
        /// Data was already stamped in Start() — zero work at death time.
        /// </summary>
        private void HandleDeath()
        {
            if (_pickup == null) return;

            // Scatter around the death position
            Vector2 scatter = Random.insideUnitCircle.normalized * _scatterRadius;
            _pickup.transform.position = transform.position + new Vector3(scatter.x, scatter.y, 0f);

            // Reveal the pickup
            _pickup.gameObject.SetActive(true);
        }
    }
}
