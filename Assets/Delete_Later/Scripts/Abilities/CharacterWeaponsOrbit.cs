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
    ///
    /// Animator parameters : Orbiting (bool)
    /// </summary>
    [AddComponentMenu("TopDown Engine/Character/Abilities/Character Weapons Orbit")]
    public class CharacterWeaponsOrbit : CharacterAbility
    {
        // ─── HelpBox ──────────────────────────────────────────────────────────────

        public override string HelpBoxText() =>
            "Manages a ring of orbiting swords around the player. " +
            "Assign WeaponPrefab in the Inspector. " +
            "Add movement/condition states to the Blocking lists to pause orbit automatically.";

        // ─── Enums ────────────────────────────────────────────────────────────────

        /// the possible states of the orbit system
        public enum OrbitState { Idle, Spawning, Sweeping, Orbiting }

        // ─── Inspector Fields ─────────────────────────────────────────────────────
        //
        // Groups follow the TDE convention: each Header covers one concern.
        // Fields within a group go: most-important first, then fine-tuning,
        // then conditional fields (MMCondition) last so they appear beneath
        // the toggle that controls their visibility.
        //
        // Group order:
        //   1. Weapon Setup     — what to spawn and how many
        //   2. Orbit Motion     — how the ring moves
        //   3. Spawn Sweep      — how swords animate into position
        //   4. Rendering        — visual presentation
        //   5. Pick Conditions  — who can interact / trigger the system
        // ─────────────────────────────────────────────────────────────────────────

        // ── 1. Weapon Setup ───────────────────────────────────────────────────────

        [Header("Weapon Setup")]

        /// the sword prefab to instantiate and orbit around the player
        [Tooltip("The sword prefab to orbit. Assign once — never delete this script or the reference is lost.")]
        public GameObject WeaponPrefab;

        /// the ScriptableObject that defines this character's weapon type (sprite, stats, tint)
        [Tooltip("Defines weapon type for this character. Leave null to use the prefab's default values.")]
        public OrbitWeaponDefinition WeaponDefinition;

        /// how many swords orbit the player at start and after respawn
        [Tooltip("How many swords orbit the player at game start and after respawn.")]
        public int WeaponCount = 3;

        // ── 2. Orbit Motion ───────────────────────────────────────────────────────

        [Header("Orbit Motion")]

        /// distance from the player center to each sword
        [Tooltip("Distance from the player center to each orbiting sword (world units).")]
        public float OrbitRadius = 1.5f;

        /// degrees per second the orbit ring rotates
        [Tooltip("Degrees per second the orbit ring spins. 180 = half rotation per second.")]
        public float OrbitSpeed = 180f;

        // ── 3. Spawn Sweep ────────────────────────────────────────────────────────

        [Header("Spawn Sweep")]

        /// the angle offset at which swords spawn before sweeping to their orbit positions
        [Tooltip("The angle (degrees) at which all swords spawn before sweeping outward.")]
        public float SpawnAngleOffset = -45f;

        /// how long (seconds) each sword takes to sweep from spawn to its orbit position
        [Tooltip("Duration in seconds for swords to sweep into their orbit position on spawn.")]
        public float ArrivalDuration = 0.5f;

        /// animation curve controlling the sweep easing
        [Tooltip("Easing curve for the spawn sweep animation. Left = start, Right = end.")]
        public AnimationCurve SweepCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // ── 4. Rendering ──────────────────────────────────────────────────────────

        [Header("Rendering")]

        /// sorting order applied to each sword's SpriteRenderer — ensures swords
        /// draw above ground tiles and enemies
        [Tooltip("Sorting order for sword SpriteRenderers. Higher = drawn on top.")]
        public int WeaponSortingOrder = 100;

        /// rotation offset applied to each sword so the blade tip points outward.
        /// Adjust this if your sword sprite's tip faces a different direction.
        [Tooltip("Rotation offset (degrees) so the blade tip faces outward. " +
                 "Change this if the sprite's tip points in a different direction.")]
        public float WeaponRotationOffset = -45f;

        // ── 5. Pick Conditions ────────────────────────────────────────────────────

        [Header("Pick Conditions")]

        /// if true, the orbit system only activates for objects with a Character component
        [Tooltip("If true, only objects with a Character component can trigger pickup callbacks.")]
        public bool RequireCharacterComponent = true;

        /// if true, the orbit system only activates for Player-type characters, not AI
        [MMCondition("RequireCharacterComponent", true)]
        [Tooltip("If true, only Player-type Characters trigger pickup callbacks (not AI enemies).")]
        public bool RequirePlayerType = true;

        // ─── Animator Parameters ──────────────────────────────────────────────────
        // TDE convention: one const string + one cached int hash per parameter.

        /// the name of the Orbiting animator bool parameter
        protected const string _orbitingAnimationParameterName = "Orbiting";
        /// cached hash — faster than a string lookup every frame
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

        /// read-only snapshot of all active sword GameObjects.
        /// Backed by a reused buffer — safe to iterate every frame with zero GC.
        public IReadOnlyList<GameObject> Weapons
        {
            get
            {
                _weaponsReadOnlyBuffer.Clear();
                foreach (var entry in _activeWeapons)
                    _weaponsReadOnlyBuffer.Add(entry.Go);
                return _weaponsReadOnlyBuffer;
            }
        }

        // ─── Constants ────────────────────────────────────────────────────────────

        /// minimum angular distance (degrees) before the sweep animation is skipped
        protected const float MinSweepAngle = 0.5f;
        /// name of the OrbitPivot child transform
        protected const string PivotName = "OrbitPivot";

        // ─── Internal State ───────────────────────────────────────────────────────

        /// pairs a weapon GameObject with its OrbitWeaponCombat component
        protected struct WeaponEntry
        {
            public GameObject        Go;
            public OrbitWeaponCombat Behaviour;
        }

        /// all currently active (visible) swords in the orbit ring
        protected List<WeaponEntry>  _activeWeapons         = new List<WeaponEntry>();
        /// inactive swords stored for reuse — avoids Instantiate/Destroy GC spikes
        protected Queue<GameObject>  _weaponPool            = new Queue<GameObject>();
        /// reused buffer for the Weapons property — avoids per-frame GC allocation
        protected List<GameObject>   _weaponsReadOnlyBuffer = new List<GameObject>();
        /// the invisible child Transform whose Z rotation changes every frame
        protected Transform          _pivot;
        /// current state of the orbit state machine
        protected OrbitState         _orbitState            = OrbitState.Idle;
        /// how many swords have finished their sweep coroutine
        protected int                _weaponsArrived;
        /// whether the pivot should rotate this frame
        protected bool               _isRotating;

        // ─── CharacterAbility Overrides ───────────────────────────────────────────

        /// <summary>
        /// Called once by TDE when the scene starts (equivalent to Start()).
        /// base.Initialization() MUST be called first — it wires _character,
        /// _movement, _condition, _health, _inputManager, _animator.
        /// </summary>
        protected override void Initialization()
        {
            base.Initialization();
            ResolveWeaponPrefab();

            if (WeaponPrefab == null)
            {
                Debug.LogError("[CharacterWeaponsOrbit] WeaponPrefab is null. " +
                               "Assign OrbitSword.prefab in the Inspector.", this);
                return;
            }

            _pivot = GetOrCreatePivot();
            UpdateWeapons(WeaponCount);
        }

        /// <summary>
        /// Called every frame by TDE's Character component (replaces Update()).
        /// Rotates the OrbitPivot child — never the player root.
        /// AbilityAuthorized checks BlockingMovementStates + BlockingConditionStates
        /// + AbilityPermitted. Add states to those Inspector lists to pause orbit
        /// automatically (e.g. Dashing, Dead).
        /// </summary>
        public override void ProcessAbility()
        {
            base.ProcessAbility();
            PerformOrbit();
        }

        /// <summary>
        /// Orbit is passive — no input required.
        /// Override here to add a toggle button (e.g. pause orbit on hold).
        /// </summary>
        protected override void HandleInput()
        {
            // Intentionally empty — orbit runs passively.
        }

        /// <summary>
        /// Registers the Orbiting bool with the character's Animator.
        /// TDE checks whether the parameter exists before setting it — no errors
        /// if the Animator does not have an Orbiting parameter.
        /// </summary>
        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(
                _orbitingAnimationParameterName,
                AnimatorControllerParameterType.Bool,
                out _orbitingAnimationParameter);
        }

        /// <summary>
        /// Pushes the current orbit state to the Animator once per cycle.
        /// Called by TDE after Early/Process/Late each frame.
        /// </summary>
        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(
                _animator,
                _orbitingAnimationParameter,
                (_isRotating && _activeWeapons.Count > 0),
                _character._animatorParameters);
        }

        /// <summary>
        /// Called automatically by TDE when the character's health reaches 0.
        /// base.OnDeath() stops ability SFX/feedbacks.
        /// </summary>
        protected override void OnDeath()
        {
            base.OnDeath();
            _isRotating = false;
            SetImmunity(false);
        }

        /// <summary>
        /// Called automatically by TDE when the character takes any damage.
        /// Reserved for future hit-flash or orbit-interrupt effects.
        /// </summary>
        protected override void OnHit()
        {
            base.OnHit();
        }

        /// <summary>
        /// Called automatically by TDE when the character respawns.
        /// Restores the configured starting sword count.
        /// </summary>
        protected override void OnRespawn()
        {
            base.OnRespawn();
            UpdateWeapons(WeaponCount);
        }

        /// <summary>
        /// Called by TDE on death before respawn — prepares clean state for next life.
        /// Distinct from OnDeath: OnDeath reacts to the event; ResetAbility prepares
        /// for the next life (mirrors TDE's own ability resets on respawn).
        /// </summary>
        public override void ResetAbility()
        {
            base.ResetAbility();
            ReturnAllWeaponsToPool();
        }

        /// <summary>
        /// Unity: called when this GameObject is destroyed (scene unload / quit).
        /// Pooled objects are inactive so Unity will not auto-destroy them —
        /// we clean them up manually to avoid leaked objects in the editor.
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
        /// Safe to call with count = 0 (no-op).
        /// Typically called by WeaponPickup when the player collects a dropped sword.
        /// </summary>
        public virtual void AddWeapons(int count)
        {
            if (count <= 0) return;
            UpdateWeapons(_activeWeapons.Count + count);
        }

        /// <summary>
        /// Rebuilds the orbit ring with a new sword count.
        /// Returns all current swords to the pool, then spawns the new set.
        /// Passing 0 clears all swords and leaves the player vulnerable.
        /// </summary>
        public virtual void UpdateWeapons(int newWeaponCount)
        {
            StopAllCoroutines();
            WeaponCount     = Mathf.Max(0, newWeaponCount);
            _weaponsArrived = 0;
            ReturnAllWeaponsToPool();
            ChangeOrbitState(OrbitState.Spawning);

            // Edge case: 0 swords → OnWeaponArrived never fires → player stays
            // vulnerable (no immunity), which is intentional.
            if (WeaponCount > 0)
                SpawnAndAnimateWeapons();
        }

        /// <summary>Starts pivot rotation.</summary>
        public virtual void StartOrbit() => _isRotating = true;

        /// <summary>Stops pivot rotation.</summary>
        public virtual void StopOrbit()  => _isRotating = false;

        // ─── Core: Orbit ──────────────────────────────────────────────────────────

        /// <summary>
        /// Applies the per-frame pivot rotation after all guard checks pass.
        /// Mirrors the TDE DoSomething() template exactly:
        ///   1. AbilityAuthorized (blocking states + AbilityPermitted)
        ///   2. Condition state check
        ///   3. Perform action
        /// </summary>
        protected virtual void PerformOrbit()
        {
            // AbilityAuthorized evaluates BlockingMovementStates[],
            // BlockingConditionStates[], and AbilityPermitted in one call —
            // all configurable from the Inspector with zero code changes.
            if (!AbilityAuthorized)
                return;

            // Secondary condition guard — mirrors TDE template exactly.
            // _condition is wired by base.Initialization().
            if (_condition.CurrentState != CharacterStates.CharacterConditions.Normal)
                return;

            if (_isRotating && _pivot != null && _activeWeapons.Count > 0)
                _pivot.Rotate(0f, 0f, OrbitSpeed * Time.deltaTime, Space.Self);
        }

        // ─── Internal: Pool ───────────────────────────────────────────────────────

        /// <summary>
        /// Deactivates all active swords, unsubscribes their events, and returns
        /// them to the pool for reuse. Also removes the sword immunity shield.
        /// </summary>
        protected virtual void ReturnAllWeaponsToPool()
        {
            foreach (var entry in _activeWeapons)
            {
                if (entry.Go == null) continue;
                UnsubscribeWeapon(entry.Behaviour);
                entry.Go.SetActive(false);
                _weaponPool.Enqueue(entry.Go);
            }
            _activeWeapons.Clear();
            _isRotating = false;
            SetImmunity(false);
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

                StartCoroutine(SweepWeaponToPosition(
                    weapon, SpawnAngleOffset, targetAngle, angularDistance));
            }
            ChangeOrbitState(OrbitState.Sweeping);
        }

        /// <summary>
        /// Retrieves a pooled sword or instantiates a new one, parents it to the
        /// pivot, positions it on the orbit circle, and wires up all components.
        /// </summary>
        protected virtual GameObject GetOrCreateWeapon(float spawnAngle, float targetAngle)
        {
            if (WeaponPrefab == null)
            {
                Debug.LogError("[CharacterWeaponsOrbit] WeaponPrefab is null.", this);
                return null;
            }

            // Pull from pool; skip any null entries that may have been destroyed
            // externally (scene unload edge case).
            GameObject weapon = null;
            while (_weaponPool.Count > 0 && weapon == null)
                weapon = _weaponPool.Dequeue();
            if (weapon == null)
                weapon = Instantiate(WeaponPrefab);

            // Parent to pivot — worldPositionStays=false keeps the sword at the
            // player's origin rather than inheriting the pivot's world position.
            weapon.transform.SetParent(_pivot, false);
            weapon.transform.localPosition = OrbitPosition(spawnAngle);
            weapon.transform.localRotation = Quaternion.Euler(0f, 0f, spawnAngle + WeaponRotationOffset);

            weapon.tag = ResolveOwnerTag();

            var wb = weapon.GetComponent<OrbitWeaponCombat>();
            if (wb != null)
            {
                ApplyWeaponDefinition(wb);
                wb.OnDestroyed += HandleWeaponDestroyed;
            }

            EnsureKinematicRigidbody(weapon);
            ApplyWeaponSortingOrder(weapon);

            weapon.SetActive(true);
            _activeWeapons.Add(new WeaponEntry { Go = weapon, Behaviour = wb });
            return weapon;
        }

        /// <summary>
        /// Converts a circle angle in degrees to a local XY position on the orbit ring.
        /// Standard unit circle math: x = r·cos(θ), y = r·sin(θ).
        /// </summary>
        protected virtual Vector3 OrbitPosition(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector3(
                OrbitRadius * Mathf.Cos(rad),
                OrbitRadius * Mathf.Sin(rad),
                0f);
        }

        // ─── Coroutines ───────────────────────────────────────────────────────────

        /// <summary>
        /// Animates a sword from startAngle to targetAngle over ArrivalDuration seconds,
        /// using SweepCurve for easing. Calls OnWeaponArrived when complete.
        /// </summary>
        protected virtual IEnumerator SweepWeaponToPosition(
            GameObject weapon,
            float startAngle,
            float targetAngle,
            float angularDistance)
        {
            // Null check: weapon may have been returned to pool before coroutine runs.
            if (weapon == null) { OnWeaponArrived(); yield break; }

            // Skip animation for negligibly short arcs.
            if (angularDistance < MinSweepAngle)
            {
                SetWeaponTransform(weapon, targetAngle);
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
                SetWeaponTransform(weapon, currentAngle);
                yield return null;
            }

            // Snap to exact final position to eliminate float drift.
            if (weapon != null)
                SetWeaponTransform(weapon, targetAngle);

            OnWeaponArrived();
        }

        /// <summary>
        /// Called by each sword when it finishes its sweep.
        /// Starts rotation and grants immunity once all swords have arrived.
        /// </summary>
        protected virtual void OnWeaponArrived()
        {
            _weaponsArrived++;
            if (_weaponsArrived < WeaponCount) return;

            OnSweepComplete?.Invoke();
            ChangeOrbitState(OrbitState.Orbiting);
            StartOrbit();
            // Full ring in orbit — shield the owner.
            // ImmuneToDamage is used instead of Invulnerable to avoid conflicting
            // with TDE's own post-hit invincibility coroutine, which also writes
            // to Invulnerable. ImmuneToDamage is checked first in
            // Health.CanTakeDamageThisFrame() and is never touched by TDE internally.
            SetImmunity(true);
        }

        // ─── Event Handlers ───────────────────────────────────────────────────────

        /// <summary>
        /// Called when a weapon's OrbitWeaponCombat fires its OnDestroyed event.
        /// Removes the sword from the active list and propagates the event upward.
        /// </summary>
        protected virtual void HandleWeaponDestroyed(GameObject weapon)
        {
            if (weapon == null) return;

            for (int i = 0; i < _activeWeapons.Count; i++)
            {
                if (_activeWeapons[i].Go != weapon) continue;
                UnsubscribeWeapon(_activeWeapons[i].Behaviour);
                _activeWeapons.RemoveAt(i);
                break;
            }

            OnWeaponDestroyed?.Invoke(weapon);

            // Last sword gone — remove immunity.
            if (_activeWeapons.Count == 0)
                SetImmunity(false);
        }

        // ─── State Machine ────────────────────────────────────────────────────────

        /// <summary>
        /// Transitions the orbit state machine to a new state.
        /// Stops rotation when entering Idle or Spawning.
        /// No-op if already in the requested state (prevents redundant transitions).
        /// </summary>
        protected virtual void ChangeOrbitState(OrbitState newState)
        {
            if (_orbitState == newState) return;
            _orbitState = newState;

            if (newState == OrbitState.Idle || newState == OrbitState.Spawning)
                StopOrbit();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the existing OrbitPivot child or creates one.
        /// Reusing the existing pivot survives domain reloads and recompiles
        /// without losing the pivot's child GameObjects.
        /// </summary>
        protected virtual Transform GetOrCreatePivot()
        {
            Transform existing = transform.Find(PivotName);
            if (existing != null) return existing;

            var go = new GameObject(PivotName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        /// <summary>
        /// Centralises weapon prefab resolution.
        /// Falls back to the asset database in the editor and Resources in builds.
        /// </summary>
        protected virtual void ResolveWeaponPrefab()
        {
            if (WeaponPrefab != null) return;

#if UNITY_EDITOR
            WeaponPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/RingOfEldenSwords/Prefabs/Weapons/OrbitSword.prefab");
#else
            WeaponPrefab = Resources.Load<GameObject>("OrbitSword");
#endif
        }

        /// <summary>
        /// Returns the correct TDE faction tag for the sword based on the owner's
        /// CharacterType. Matches the tags TDE uses everywhere for faction checks.
        /// </summary>
        protected virtual string ResolveOwnerTag()
        {
            return (_character.CharacterType ==
                    MoreMountains.TopDownEngine.Character.CharacterTypes.Player)
                ? "Player"
                : "Enemy";
        }

        /// <summary>
        /// Applies WeaponDefinition to the combat component, or resets its health
        /// to prefab defaults when no definition is assigned.
        /// </summary>
        protected virtual void ApplyWeaponDefinition(OrbitWeaponCombat wb)
        {
            if (WeaponDefinition != null)
                wb.ApplyDefinition(WeaponDefinition);
            else
                wb.ResetHealth();
        }

        /// <summary>
        /// Ensures the sword's Rigidbody2D is Kinematic so physics cannot fling it
        /// when the player moves. Adds the component if missing.
        /// </summary>
        protected virtual void EnsureKinematicRigidbody(GameObject weapon)
        {
            Rigidbody2D rb = weapon.GetComponent<Rigidbody2D>();
            if (rb == null) rb = weapon.AddComponent<Rigidbody2D>();
            rb.bodyType     = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.simulated    = true;
        }

        /// <summary>
        /// Sets the sorting order on the first SpriteRenderer found in the weapon's
        /// children. Uses the Inspector-configurable WeaponSortingOrder value.
        /// No-op if no SpriteRenderer exists.
        /// </summary>
        protected virtual void ApplyWeaponSortingOrder(GameObject weapon)
        {
            var sr = weapon.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = WeaponSortingOrder;
        }

        /// <summary>
        /// Null-safe setter for the owner's ImmuneToDamage flag.
        /// All immunity changes go through here so there is one place to update
        /// if the immunity mechanism changes in future.
        /// </summary>
        protected virtual void SetImmunity(bool immune)
        {
            if (_health != null)
                _health.ImmuneToDamage = immune;
        }

        /// <summary>
        /// Centralised OnDestroyed unsubscription — avoids duplicate -= calls
        /// scattered across ReturnAllWeaponsToPool and HandleWeaponDestroyed.
        /// </summary>
        protected virtual void UnsubscribeWeapon(OrbitWeaponCombat wb)
        {
            if (wb != null)
                wb.OnDestroyed -= HandleWeaponDestroyed;
        }

        /// <summary>
        /// Applies localPosition and localRotation to a weapon at a given orbit angle.
        /// Single point of change if WeaponRotationOffset formula ever changes.
        /// </summary>
        protected virtual void SetWeaponTransform(GameObject weapon, float angle)
        {
            weapon.transform.localPosition = OrbitPosition(angle);
            weapon.transform.localRotation = Quaternion.Euler(0f, 0f, angle + WeaponRotationOffset);
        }

        // ─── Editor ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Clamps Inspector values to valid ranges on every Inspector change.
        /// Prevents divide-by-zero (ArrivalDuration) and negative counts/distances.
        /// </summary>
        protected virtual void OnValidate()
        {
            OrbitRadius     = Mathf.Max(0.1f,  OrbitRadius);
            WeaponCount     = Mathf.Max(0,      WeaponCount);
            OrbitSpeed      = Mathf.Max(0f,     OrbitSpeed);
            ArrivalDuration = Mathf.Max(0.01f,  ArrivalDuration);
        }
    }
}
