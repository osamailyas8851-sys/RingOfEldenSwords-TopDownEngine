using System.Collections;
using MoreMountains.Tools;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Weapons;
using UnityEngine;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Extends Loot with two behaviours:
    ///
    /// 1. Pool warmup — on Start, one pooled loot object is activated for one frame
    ///    so that Awake/Start (TMP atlas init, MMFeedbacks, etc.) run invisibly at
    ///    scene load instead of causing a lag spike on the first enemy death.
    ///
    /// 2. Weapon data bridging — overrides Spawn() so that immediately after the
    ///    pooled pickup is retrieved, WeaponDefinition and ActiveWeaponCount are read
    ///    from CharacterWeaponsOrbit on this same GameObject and forwarded to the
    ///    pickup via ApplyWeaponDataToPickup(). This keeps the drop always in sync
    ///    with what the enemy was carrying at the moment of death — no duplicate
    ///    Inspector data required.
    ///
    /// Use this component instead of Loot on any enemy prefab that has both
    /// PoolLoot enabled and a CharacterWeaponsOrbit component.
    /// </summary>
    public class LootExtended : Loot
    {
        // ─── Cached References ────────────────────────────────────────────────────

        /// cached reference to the orbit ability on this same GameObject.
        /// Populated in Start() — valid for the entire session.
        protected CharacterWeaponsOrbit _weaponsOrbit;

        // ─── Lifecycle ────────────────────────────────────────────────────────────

        protected virtual void Start()
        {
            _weaponsOrbit = GetComponent<CharacterWeaponsOrbit>();

            if (PoolLoot)
                StartCoroutine(WarmupPooledLoot());
        }

        // ─── Spawn Override ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by SpawnOneLoot() for every loot drop.
        /// Calls base.Spawn() first so _spawnedObject is populated (pooled or
        /// instantiated), then reads weapon data from CharacterWeaponsOrbit and
        /// forwards it to the spawned pickup via ApplyWeaponDataToPickup().
        /// </summary>
        protected override void Spawn(GameObject gameObjectToSpawn)
        {
            // Let the base class retrieve/instantiate the pickup into _spawnedObject.
            base.Spawn(gameObjectToSpawn);

            if (_spawnedObject == null) return;
            if (_weaponsOrbit  == null) return;

            // Read the Inspector-configured values from the orbit ability:
            //   WeaponDefinition — the ScriptableObject (sprite, stats, tint)
            //   WeaponCount      — the starting count set in the Inspector;
            //                      ActiveWeaponCount is always 0 by the time
            //                      the enemy dies because swords are defeated first
            ApplyWeaponDataToPickup(
                _spawnedObject,
                _weaponsOrbit.WeaponDefinition,
                _weaponsOrbit.WeaponCount);
        }

        /// <summary>
        /// Forwards weapon data to the spawned pickup by calling Initialize()
        /// on its PickableItemExtended component.
        /// </summary>
        protected virtual void ApplyWeaponDataToPickup(
            GameObject pickup,
            OrbitWeaponDefinition definition,
            int count)
        {

            var picker = pickup.GetComponent<PickableItemExtended>();
            if (picker != null) picker.Initialize(definition, count);
        }

        // ─── Pool Warmup ──────────────────────────────────────────────────────────

        /// <summary>
        /// Activates one pooled loot object for one frame then deactivates it,
        /// forcing its initialization code (TMP atlas, MMFeedbacks, etc.) to run
        /// at scene load time rather than on the first enemy death.
        /// One object is sufficient — TMP font atlas init is global and cached after
        /// the first activation, making all subsequent spawns lag-free.
        /// </summary>
        protected virtual IEnumerator WarmupPooledLoot()
        {
            MMPoolableObject[] pooled = null;

            if (LootMode == LootModes.Unique && _simplePooler != null)
                pooled = _simplePooler.GetComponentsInChildren<MMPoolableObject>(true);
            else if (_multipleObjectPooler != null)
                pooled = _multipleObjectPooler.GetComponentsInChildren<MMPoolableObject>(true);

            if (pooled == null || pooled.Length == 0)
                yield break;

            pooled[0].gameObject.SetActive(true);
            yield return null;
            pooled[0].gameObject.SetActive(false);
        }
    }
}
