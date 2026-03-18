using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.Character.Abilities
{
    /// <summary>
    /// Manages a ring of orbiting swords around the player character.
    /// 
    /// Architecture: a dedicated OrbitPivot child GameObject rotates every frame.
    /// Only the pivot rotates — the player root and sprite are never touched.
    /// Swords are children of OrbitPivot and follow both its rotation and
    /// the player's world position automatically via Unity's parent transform.
    /// 
    /// Add this component to the Player prefab root.
    /// Assign WeaponPrefab in the Inspector — never delete this script file
    /// or the serialized reference will be lost.
    /// </summary>
    [AddComponentMenu("TopDown Engine/Character/Abilities/Character Orbit Weapons")]
    public class CharacterOrbitWeapons : CharacterAbility
    {
        /// <summary>
        /// Displays a description at the top of this component's Inspector panel.
        /// </summary>
        public override string HelpBoxText()
        {
            return "Manages a ring of orbiting swords around the player. " +
                   "Assign WeaponPrefab in the Inspector. " +
                   "Add movement/condition states to the Blocking lists to pause orbit automatically.";
        }

        // ─── Enums ────────────────────────────────────────────────────────────────

        /// the possible states of the orbit system
        public enum OrbitState { Idle, Spawning, Sweeping, Orbiting }

        // ─── Inspector Fields ─────────────────────────────────────────────────────

        [Header("Orbit Settings")]
        /// the sword prefab to instantiate and orbit around the player
        [Tooltip("The sword prefab to orbit. Assign once — never delete this script or the reference is lost.")]
        public GameObject WeaponPrefab;

        /// how many swords orbit the player at start and after respawn
        [Tooltip("How many swords orbit the player at game start and after respawn.")]
        public int WeaponCount = 3;

        /// distance from the player center to each sword
        [Tooltip("Distance from the player center to each orbiting sword (world units).")]
        public float OrbitRadius = 2f;

        /// degrees per second the orbit ring rotates
        [Tooltip("Degrees per second the orbit ring spins. 180 = half rotation per second.")]
        public float OrbitSpeed = 180f;

        [Header("Spawn Sweep")]
        /// how long (seconds) each sword takes to sweep from spawn to its orbit position
        [Tooltip("Duration in seconds for swords to sweep into their orbit position on spawn.")]
        public float ArrivalDuration = 0.5f;

        /// the angle offset at which swords spawn before sweeping to their orbit positions
        [Tooltip("The angle (degrees) at which all swords spawn before sweeping outward.")]
        public float SpawnAngleOffset = -45f;

        /// animation curve controlling the sweep easing
        [Tooltip("Easing curve for the spawn sweep animation.")]
        public AnimationCurve SweepCurve = AnimationCurve.Linear(0, 0, 1, 1);

        // ─── Animation Parameters ─────────────────────────────────────────────────
        // Following TDE template: register a parameter name + cached hash

        /// the name of the Orbiting animator bool parameter
        protected const string _orbitingAnimationParameterName = "Orbiting";
        /// the hashed version of the Orbiting animator parameter (faster than string lookup)
        protected int _orbitingAnimationParameter;

        // ─── Events ───────────────────────────────────────────────────────────────

        /// raised when a sword is destroyed mid-orbit
        public event Action<GameObject> OnWeaponDestroyed;
        /// raised when all swords finish their sweep and begin orbiting
        public event Action OnSweepComplete;

        // ─── Public Read-Only Properties ──────────────────────────────────────────

        /// the current state of the orbit system
        public OrbitState CurrentOrbitState => _orbitState;
        /// whether the orbit pivot is currently spinning
        public bool IsOrbiting => _isRotating;
        /// how many swords are currently active in the orbit ring
        public int ActiveWeaponCount => _activeWeapons.Count;

        /// read-only list of all active sword GameObjects
        public IReadOnlyList<GameObject> Weapons
        {
            get
            {
                var list = new List<GameObject>(_activeWeapons.Count);
                foreach (var entry in _activeWeapons) list.Add(entry.Go);
                return list;
            }
        }

        // ─── Constants ────────────────────────────────────────────────────────────

        /// rotation offset so the blade tip points outward for the current sword sprite
        protected const float WeaponRotationOffset = -45f;
        /// sorting order ensuring swords render above ground and enemies
        protected const int WeaponSortingOrder = 100;

        // ─── Internal State ───────────────────────────────────────────────────────

        /// pairs a sword GameObject with its optional WeaponBehaviour component
        protected struct WeaponEntry
        {
            public GameObject      Go;
            public WeaponBehaviour Behaviour;
        }

        /// all currently active (visible) swords in the orbit ring
        protected List<WeaponEntry>  _activeWeapons = new List<WeaponEntry>();
        /// inactive swords stored for reuse — avoids Instantiate/Destroy GC spikes
        protected Queue<GameObject>  _weaponPool    = new Queue<GameObject>();
        /// the invisible child Transform whose Z rotation changes every frame
        protected Transform          _pivot;
        /// current state of the orbit state machine
        protected OrbitState         _orbitState    = OrbitState.Idle;
        /// how many swords have finished their sweep coroutine
        protected int                _weaponsArrived;
        /// whether the pivot should rotate this frame
        protected bool               _isRotating;

        // ─── CharacterAbility Overrides ───────────────────────────────────────────

        /// <summary>
        /// Called once by TDE when the scene starts (equivalent to Start()).
        /// Grabs components, creates the orbit pivot, and spawns starting swords.
        /// base.Initialization() MUST be called first — it wires up
        /// _character, _movement, _condition, _health, _inputManager, _animator.
        /// </summary>
        protected override void Initialization()
        {
            base.Initialization();

            // Fallback: load WeaponPrefab by path if the Inspector reference was lost
            // (happens when script GUID changes after delete/recreate)
            #if UNITY_EDITOR
            if (WeaponPrefab == null)
                WeaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/RingOfEldenSwords/Prefabs/Weapons/OrbitSword.prefab");
            #else
            if (WeaponPrefab == null)
                WeaponPrefab = Resources.Load<GameObject>("OrbitSword");
            #endif

            if (WeaponPrefab == null)
            {
                Debug.LogError("[CharacterOrbitWeapons] WeaponPrefab is null. " +
                               "Assign OrbitSword.prefab in the Inspector.", this);
                return;
            }

            _pivot = GetOrCreatePivot();
            UpdateWeapons(WeaponCount);
        }

        /// <summary>
        /// Called every frame by TDE's Character component (replaces Update()).
        /// Rotates the OrbitPivot child — never the player root.
        /// AbilityAuthorized checks BlockingMovementStates + BlockingConditionStates + AbilityPermitted.
        /// Add states to those Inspector lists to automatically pause orbit (e.g. Dashing).
        /// </summary>
        public override void ProcessAbility()
        {
            base.ProcessAbility();
            PerformOrbit();
        }

        /// <summary>
        /// Called at the start of each ability cycle to handle player input.
        /// Orbit is passive — no input required. Override here if you want
        /// a button to toggle orbit speed or pause it manually.
        /// </summary>
        protected override void HandleInput()
        {
            // Orbit runs automatically — no input needed.
        }

        /// <summary>
        /// Checks state machine conditions then rotates the pivot.
        /// Follows the TDE template's DoSomething() pattern exactly.
        /// </summary>
        protected virtual void PerformOrbit()
        {
            // Guard 1: AbilityAuthorized checks all three blocking systems at once:
            //   BlockingMovementStates[] (e.g. Dashing) 
            //   BlockingConditionStates[] (e.g. Dead, Stunned)
            //   AbilityPermitted bool
            // These are all set from the Inspector — zero code changes needed.
            if (!AbilityAuthorized)
            {
                return;
            }

            // Guard 2: explicit condition check (mirrors TDE template exactly)
            // _condition is wired by base.Initialization()
            if (_condition.CurrentState != CharacterStates.CharacterConditions.Normal)
            {
                return;
            }

            // Guards passed — rotate the pivot
            if (_isRotating && _pivot != null && _activeWeapons.Count > 0)
            {
                _pivot.Rotate(0f, 0f, OrbitSpeed * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>
        /// Registers the Orbiting bool with the character's Animator.
        /// TDE checks whether the parameter exists before setting it — no errors if missing.
        /// </summary>
        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(
                _orbitingAnimationParameterName,
                AnimatorControllerParameterType.Bool,
                out _orbitingAnimationParameter);
        }

        /// <summary>
        /// Sends the current orbit state to the Animator every frame.
        /// Called by TDE after Early/Process/Late process each cycle.
        /// </summary>
        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(
                _animator,
                _orbitingAnimationParameter,
                _isRotating && _activeWeapons.Count > 0,
                _character._animatorParameters);
        }

        /// <summary>
        /// Called automatically by TDE when the character's health reaches 0.
        /// base.OnDeath() stops ability SFX and feedbacks.
        /// </summary>
        protected override void OnDeath()
        {
            base.OnDeath();
            _isRotating = false;
        }

        /// <summary>
        /// Called automatically by TDE when the character takes any damage.
        /// Hook available for future effects (e.g. flash swords red on hit).
        /// </summary>
        protected override void OnHit()
        {
            base.OnHit();
        }

        /// <summary>
        /// Called automatically by TDE when the character respawns.
        /// Restores the starting sword count.
        /// </summary>
        protected override void OnRespawn()
        {
            base.OnRespawn();
            UpdateWeapons(WeaponCount);
        }

        /// <summary>
        /// Called by TDE on death before respawn — clean up state for the next life.
        /// Different from OnDeath: OnDeath reacts, ResetAbility prepares for respawn.
        /// </summary>
        public override void ResetAbility()
        {
            base.ResetAbility();
            ReturnAllWeaponsToPool();
        }

        /// <summary>
        /// Unity: called when this GameObject is destroyed (scene unload, quit).
        /// Pooled objects are inactive so Unity won't auto-destroy them.
        /// </summary>
        protected virtual void OnDestroy()
        {
            while (_weaponPool.Count > 0)
            {
                var pooled = _weaponPool.Dequeue();
                if (pooled != null) Destroy(pooled);
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Adds swords to the current orbit ring.
        /// Called by WeaponPickup when the player collects a dropped sword.
        /// </summary>
        public virtual void AddWeapons(int count)
        {
            if (count <= 0) return;
            UpdateWeapons(_activeWeapons.Count + count);
        }

        /// <summary>
        /// Rebuilds the orbit ring with a new sword count.
        /// Returns all current swords to the pool, then spawns the new set.
        /// </summary>
        public virtual void UpdateWeapons(int newWeaponCount)
        {
            StopAllCoroutines();
            WeaponCount      = newWeaponCount;
            _weaponsArrived  = 0;
            ReturnAllWeaponsToPool();
            ChangeOrbitState(OrbitState.Spawning);
            SpawnAndAnimateWeapons();
        }

        /// <summary>Starts pivot rotation.</summary>
        public virtual void StartOrbit() => _isRotating = true;
        /// <summary>Stops pivot rotation.</summary>
        public virtual void StopOrbit()  => _isRotating = false;

        // ─── Internal: Pool ───────────────────────────────────────────────────────

        /// <summary>
        /// Deactivates all active swords and returns them to the pool for reuse.
        /// </summary>
        protected virtual void ReturnAllWeaponsToPool()
        {
            foreach (var entry in _activeWeapons)
            {
                if (entry.Go == null) continue;
                if (entry.Behaviour != null)
                    entry.Behaviour.OnDestroyed -= HandleWeaponDestroyed;
                entry.Go.SetActive(false);
                _weaponPool.Enqueue(entry.Go);
            }
            _activeWeapons.Clear();
            _isRotating = false;
        }

        // ─── Internal: Spawn & Sweep ──────────────────────────────────────────────

        /// <summary>
        /// Spawns WeaponCount swords evenly spaced around the orbit circle,
        /// each starting a sweep coroutine to animate into position.
        /// </summary>
        protected virtual void SpawnAndAnimateWeapons()
        {
            if (WeaponCount <= 0 || _pivot == null) return;

            float angleStep = 360f / WeaponCount;
            for (int i = 0; i < WeaponCount; i++)
            {
                float targetAngle = i * angleStep;
                var   weapon      = GetOrCreateWeapon(SpawnAngleOffset, targetAngle);
                if (weapon == null) continue;

                float angularDistance = targetAngle - SpawnAngleOffset;
                if (angularDistance < 0f) angularDistance += 360f;
                StartCoroutine(SweepWeaponToPosition(weapon, SpawnAngleOffset, targetAngle, angularDistance));
            }
            ChangeOrbitState(OrbitState.Sweeping);
        }

        /// <summary>
        /// Retrieves a pooled sword or instantiates a new one, then positions it
        /// on the orbit circle at spawnAngle and sets up its components.
        /// </summary>
        protected virtual GameObject GetOrCreateWeapon(float spawnAngle, float targetAngle)
        {
            if (WeaponPrefab == null)
            {
                Debug.LogError("[CharacterOrbitWeapons] WeaponPrefab is null.", this);
                return null;
            }

            // Reuse a pooled sword if available, otherwise instantiate a new one
            GameObject weapon = null;
            while (_weaponPool.Count > 0 && weapon == null)
                weapon = _weaponPool.Dequeue();
            if (weapon == null)
                weapon = Instantiate(WeaponPrefab);

            // Parent to pivot with worldPositionStays=false so it sits at player origin
            weapon.transform.SetParent(_pivot, false);
            weapon.transform.localPosition = OrbitPosition(spawnAngle);
            weapon.transform.localRotation = Quaternion.Euler(0f, 0f, spawnAngle + WeaponRotationOffset);

            // Wire WeaponBehaviour if present
            var wb = weapon.GetComponent<WeaponBehaviour>();
            if (wb != null)
            {
                wb.ResetHealth();
                wb.OnDestroyed += HandleWeaponDestroyed;
            }

            // Force Kinematic — Dynamic Rigidbody2D lets physics fling swords away when player moves
            // NOTE: use explicit null check, NOT ?? — Unity's GetComponent returns
            // fake-null which fools the ?? operator, causing MissingComponentException
            Rigidbody2D rb = weapon.GetComponent<Rigidbody2D>();
            if (rb == null) rb = weapon.AddComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.simulated    = true;

            // Ensure swords render above ground and enemies
            var sr = weapon.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = WeaponSortingOrder;

            weapon.SetActive(true);
            _activeWeapons.Add(new WeaponEntry { Go = weapon, Behaviour = wb });
            return weapon;
        }

        /// <summary>
        /// Converts a circle angle in degrees to a local XY position on the orbit ring.
        /// Standard circle math: x = r·cos(θ), y = r·sin(θ).
        /// </summary>
        protected virtual Vector3 OrbitPosition(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(
                OrbitRadius * Mathf.Cos(rad),
                OrbitRadius * Mathf.Sin(rad),
                0f);
        }

        // ─── Internal: Events ─────────────────────────────────────────────────────

        /// <summary>
        /// Called when a sword's WeaponBehaviour fires its OnDestroyed event.
        /// Removes the sword from the active list and fires OnWeaponDestroyed.
        /// </summary>
        protected virtual void HandleWeaponDestroyed(GameObject weapon)
        {
            if (weapon == null) return;
            for (int i = 0; i < _activeWeapons.Count; i++)
            {
                if (_activeWeapons[i].Go != weapon) continue;
                if (_activeWeapons[i].Behaviour != null)
                    _activeWeapons[i].Behaviour.OnDestroyed -= HandleWeaponDestroyed;
                _activeWeapons.RemoveAt(i);
                break;
            }
            OnWeaponDestroyed?.Invoke(weapon);
        }

        /// <summary>
        /// Transitions the orbit state machine to a new state.
        /// Stops rotation when entering Idle or Spawning.
        /// </summary>
        protected virtual void ChangeOrbitState(OrbitState newState)
        {
            if (_orbitState == newState) return;
            _orbitState = newState;
            if (newState == OrbitState.Idle || newState == OrbitState.Spawning)
                StopOrbit();
        }

        // ─── Coroutines ───────────────────────────────────────────────────────────

        /// <summary>
        /// Animates a sword from startAngle to targetAngle over ArrivalDuration seconds,
        /// using SweepCurve for easing. Calls OnWeaponArrived when complete.
        /// </summary>
        protected virtual IEnumerator SweepWeaponToPosition(
            GameObject weapon, float startAngle, float targetAngle, float angularDistance)
        {
            if (weapon == null) { OnWeaponArrived(); yield break; }

            // Skip animation for very short distances
            if (angularDistance < 0.5f)
            {
                weapon.transform.localPosition = OrbitPosition(targetAngle);
                weapon.transform.localRotation = Quaternion.Euler(0f, 0f, targetAngle + WeaponRotationOffset);
                OnWeaponArrived();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < ArrivalDuration)
            {
                if (weapon == null) { OnWeaponArrived(); yield break; }
                elapsed += Time.deltaTime;
                float t            = Mathf.Clamp01(elapsed / ArrivalDuration);
                float currentAngle = startAngle + angularDistance * SweepCurve.Evaluate(t);
                weapon.transform.localPosition = OrbitPosition(currentAngle);
                weapon.transform.localRotation = Quaternion.Euler(0f, 0f, currentAngle + WeaponRotationOffset);
                yield return null;
            }

            if (weapon != null)
            {
                weapon.transform.localPosition = OrbitPosition(targetAngle);
                weapon.transform.localRotation = Quaternion.Euler(0f, 0f, targetAngle + WeaponRotationOffset);
            }
            OnWeaponArrived();
        }

        /// <summary>
        /// Called by each sword when it finishes its sweep coroutine.
        /// When all swords have arrived, fires OnSweepComplete and starts rotation.
        /// </summary>
        protected virtual void OnWeaponArrived()
        {
            _weaponsArrived++;
            if (_weaponsArrived >= WeaponCount)
            {
                OnSweepComplete?.Invoke();
                ChangeOrbitState(OrbitState.Orbiting);
                StartOrbit();
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the existing OrbitPivot child or creates one.
        /// Reuses existing pivot to survive recompiles without losing state.
        /// </summary>
        protected virtual Transform GetOrCreatePivot()
        {
            Transform existing = transform.Find("OrbitPivot");
            if (existing != null) return existing;

            var go = new GameObject("OrbitPivot");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        // ─── Editor ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by Unity in the Editor when any Inspector value changes.
        /// Clamps WeaponCount and OrbitRadius to valid ranges.
        /// </summary>
        protected virtual void OnValidate()
        {
            WeaponCount = Mathf.Max(1, WeaponCount);
            OrbitRadius = Mathf.Max(0.1f, OrbitRadius);
        }
    }
}
