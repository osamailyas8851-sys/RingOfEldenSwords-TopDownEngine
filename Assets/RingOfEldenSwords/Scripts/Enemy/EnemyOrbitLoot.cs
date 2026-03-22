using System.Collections;
using UnityEngine;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Pickups;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.Enemy
{
    /// <summary>
    /// Attach to an enemy root alongside CharacterOrbitWeapons.
    /// Requires an OrbitWeaponPickup embedded as an inactive child of the enemy prefab.
    ///
    /// At scene load : stamps weapon data, then pre-warms PickableItem.Start() +
    ///                 MMFeedbacks.Initialization() via a two-frame coroutine so
    ///                 the first real SetActive(true) at death is lag-free.
    /// At death      : detaches, scatters, and activates the already-warmed pickup.
    /// At respawn    : re-attaches the pickup as an inactive child, ready for reuse.
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
        private bool              _warmed;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _health = GetComponent<Health>();

            // includeInactive=true required — pickup starts inactive in prefab.
            _pickup = GetComponentInChildren<OrbitWeaponPickup>(true);

            if (_pickup == null)
                Debug.LogWarning($"[EnemyOrbitLoot] No OrbitWeaponPickup child found on {gameObject.name}. " +
                                 "Add OrbitWeaponPickup as an inactive child in the prefab.", this);
        }

        protected virtual void Start()
        {
            if (_pickup == null) return;

            // Stamp weapon data — CharacterOrbitWeapons.Initialization() has run by now.
            StampPickup();

            // Pre-warm: activate for one frame so PickableItem.Start() and
            // MMFeedbacks.Initialization() run at scene-load time (invisible to
            // the player), not at death time (visible freeze).
            // Same-frame toggle is useless — Unity defers Start() to end-of-frame
            // and cancels it if the object is immediately deactivated again.
            // The coroutine ensures activation persists for at least one full frame.
            StartCoroutine(PreWarmPickup());
        }

        protected virtual void OnEnable()
        {
            if (_health != null) _health.OnDeath += HandleDeath;
        }

        protected virtual void OnDisable()
        {
            if (_health != null) _health.OnDeath -= HandleDeath;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Call from a respawn handler to reset the pickup for the next death cycle.</summary>
        public virtual void OnRespawn()
        {
            if (_pickup == null) return;
            _pickup.gameObject.SetActive(false);
            _pickup.transform.SetParent(transform);
            StampPickup();
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Activates the pickup for exactly one frame so Unity runs
        /// PickableItem.Start() and MMFeedbacks.Initialization() now,
        /// then hides it again — all during scene load, invisible to the player.
        /// </summary>
        private IEnumerator PreWarmPickup()
        {
            _pickup.gameObject.SetActive(true);   // Start() queued for this frame
            yield return null;                     // let the frame complete → Start() runs
            if (!_warmed)                          // only hide if death hasn't fired yet
                _pickup.gameObject.SetActive(false);
            _warmed = true;
        }

        /// <summary>Stamps weapon data from CharacterOrbitWeapons onto the pickup.</summary>
        private void StampPickup()
        {
            CharacterOrbitWeapons orbit = GetComponent<CharacterOrbitWeapons>();
            int                   count = (orbit != null) ? Mathf.Max(1, orbit.WeaponCount) : 1;
            OrbitWeaponDefinition def   = orbit?.WeaponDefinition;
            _pickup.Init(count, def);
        }

        /// <summary>
        /// Fires on Health.OnDeath.
        /// Detaches the pickup from the enemy, scatters it, and reveals it.
        /// MMFeedbacks is already initialized — no lag spike.
        /// </summary>
        private void HandleDeath()
        {
            if (_pickup == null) return;

            _warmed = true; // prevent PreWarm coroutine from hiding it after death

            // Detach so the pickup survives the enemy being disabled/destroyed
            _pickup.transform.SetParent(null);

            // Scatter around the death position
            Vector2 scatter = Random.insideUnitCircle.normalized * _scatterRadius;
            _pickup.transform.position = transform.position + new Vector3(scatter.x, scatter.y, 0f);

            // Reveal — MMFeedbacks already initialized, this is now lag-free
            _pickup.gameObject.SetActive(true);
        }
    }
}
