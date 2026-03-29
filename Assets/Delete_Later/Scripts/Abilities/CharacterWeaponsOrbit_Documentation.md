# CharacterWeaponsOrbit — Full Documentation

## Flow Diagram

```
SCENE LOAD
    │
    ▼
Character.Start()
    │
    ▼
Initialization()
    ├── base.Initialization()           ← wires _character, _health, _animator, _condition
    ├── ResolveWeaponPrefab()           ← fallback if Inspector ref is null
    ├── FindSwordPooler()               ← find or create shared static pool
    │       ├── scan SwordPoolers[]     ← same prefab already pooled?
    │       │       YES → return it
    │       │       NO  ↓
    │       ├── new GameObject("[MMSimpleObjectPooler] OrbitSword")
    │       ├── AddComponent<MMSimpleObjectPooler>()
    │       ├── FillObjectPool()        ← pre-creates PoolSize inactive swords
    │       └── add to SwordPoolers[]   ← register for sharing
    │
    ├── cache _waitingPoolTransform     ← where returned swords get reparented
    ├── GetOrCreatePivot()              ← invisible rotating child "OrbitPivot"
    └── UpdateWeapons(WeaponCount)      ← spawn the initial sword ring
            │
            ▼
    UpdateWeapons(count)
        ├── StopAllCoroutines()         ← cancel any in-progress sweeps
        ├── ReturnAllWeaponsToPool()    ← deactivate + reparent all swords
        ├── ChangeOrbitState(Spawning)
        └── SpawnAndAnimateWeapons()
                │
                ▼
        ┌─ for each sword (0..count-1) ─┐
        │                                │
        │   GetOrCreateWeapon()          │
        │       ├── GetPooledGameObject()│ ← fetch inactive from shared pool
        │       ├── SetParent(_pivot)    │ ← attach to rotating pivot
        │       ├── position + rotation  │
        │       ├── tag = owner faction  │
        │       ├── EnsureKinematicRb()  │
        │       ├── ApplyWeaponSorting() │
        │       ├── SetActive(true)      │ ← Awake() runs (first time only)
        │       ├── ApplyWeaponDef(wb)   │ ← sprite + stats from ScriptableObject
        │       └── wb.OnDestroyed +=    │ ← subscribe to death event
        │                                │
        │   SweepWeaponToPosition()      │ ← coroutine: animate from spawn angle
        │       └── each frame:          │
        │           position = lerp      │
        │           rotation = lerp      │
        │           yield return null    │
        │       └── OnWeaponArrived()    │
        │                                │
        └────────────────────────────────┘
                │
                ▼
        OnWeaponArrived()               (called per sword)
            │ _weaponsArrived++
            │ if all arrived:
            ├── OnSweepComplete event
            ├── ChangeOrbitState(Orbiting)
            ├── StartOrbit()            ← _isRotating = true
            └── SetImmunity(true)       ← player immune while full ring


EVERY FRAME (while alive)
    │
    ▼
Character.Update()
    │
    ▼
ProcessAbility()
    │
    ▼
PerformOrbit()
    ├── if !AbilityAuthorized → return  ← blocked by Dashing, Dead, etc.
    ├── if condition != Normal → return
    └── _pivot.Rotate(0, 0, OrbitSpeed * deltaTime)
                                        ← all swords rotate with pivot
                                           (they're children of pivot)

EVERY FRAME (animator sync)
    │
    ▼
UpdateAnimator()
    └── set "Orbiting" bool = (_isRotating && swords > 0)


SWORD DESTROYED IN COMBAT
    │
    ▼
OrbitWeaponCombat.OnDestroyed event
    │
    ▼
HandleWeaponDestroyed(weapon)
    ├── find weapon in _activeWeapons
    ├── UnsubscribeWeapon()             ← wb.OnDestroyed -= handler
    ├── RemoveAt(i)                     ← remove from active list
    ├── SetParent(_waitingPoolTransform)← reparent to pool (prevent cascade destroy)
    ├── OnWeaponDestroyed event         ← notify external listeners
    └── if _activeWeapons.Count == 0:
        └── SetImmunity(false)          ← no swords = no shield


PLAYER PICKS UP LOOT
    │
    ▼
PickableItemExtended.Pick(playerGO)
    │
    ▼
orbit.AddWeapons(count, definition)
    ├── WeaponDefinition = definition   ← upgrade sword level
    └── UpdateWeapons(active + count)   ← rebuild ring with new count + type
        (see UpdateWeapons flow above)


CHARACTER DIES
    │
    ▼
Health.OnDeath event
    │
    ├──▶ OnDeath()
    │       ├── _isRotating = false
    │       └── SetImmunity(false)
    │
    └──▶ LootExtended.OnDeath()         ← drops weapon pickup
            └── Spawn() → ApplyWeaponDataToPickup()


CHARACTER RESPAWNS
    │
    ▼
OnRespawn()
    └── UpdateWeapons(WeaponCount)      ← restore starting sword count


CHARACTER DESTROYED (scene unload / Destroy())
    │
    ▼
OnDestroy()
    └── ReturnAllWeaponsToPool()        ← reparent swords to pool before cascade
```

---

## Line-by-Line Explanation

### Lines 1-7: Using Directives

```csharp
using UnityEngine;
```
Core Unity types: `GameObject`, `Transform`, `MonoBehaviour`, `Vector3`, `Quaternion`, `Mathf`, `Debug`, `AnimationCurve`, `Space`, `Time`, `Rigidbody2D`, `RigidbodyType2D`, `SpriteRenderer`, `SceneManagement`.

```csharp
using System;
```
For `Action<T>` delegate type used in events (`OnWeaponDestroyed`, `OnSweepComplete`).

```csharp
using System.Collections;
```
For `IEnumerator` — required return type for Unity coroutines (`SweepWeaponToPosition`).

```csharp
using System.Collections.Generic;
```
For `List<T>` and `IReadOnlyList<T>` — used for `_activeWeapons`, `_weaponsReadOnlyBuffer`, `SwordPoolers`.

```csharp
using MoreMountains.Tools;
```
MM Tools library. Gives us `MMSimpleObjectPooler`, `MMObjectPool`, `MMPoolableObject`, `MMAnimatorExtensions`, `MMCondition` attribute. This is TDE's utility layer.

```csharp
using MoreMountains.TopDownEngine;
```
TDE core. Gives us `CharacterAbility` (our base class), `Character`, `CharacterStates`, `Health`, `TopDownMonoBehaviour`. Everything TDE-specific.

```csharp
using RingOfEldenSwords.Combat.Weapons;
```
Our project namespace. Gives us `OrbitWeaponDefinition` (ScriptableObject) and `OrbitWeaponCombat` (the combat component on each sword).

---

### Lines 9-26: Namespace and Class Declaration

```csharp
namespace RingOfEldenSwords.Character.Abilities
```
Our project's ability namespace. Keeps our code separate from TDE's `MoreMountains.TopDownEngine` namespace to avoid name collisions and make it clear this is project code, not engine code.

```csharp
[AddComponentMenu("TopDown Engine/Character/Abilities/Character Weapons Orbit")]
```
Tells Unity where this component appears in the Inspector's "Add Component" menu. Nesting it under "TopDown Engine/Character/Abilities/" puts it alongside TDE's built-in abilities so it's easy to find.

```csharp
public class CharacterWeaponsOrbit : CharacterAbility
```
Inherits from `CharacterAbility` — TDE's base class for all character abilities. This gives us:
- **Automatic lifecycle hooks**: `Initialization()`, `ProcessAbility()`, `HandleInput()`, `OnDeath()`, `OnHit()`, `OnRespawn()`, `ResetAbility()`
- **Built-in blocking**: `BlockingMovementStates[]` and `BlockingConditionStates[]` Inspector arrays that pause the ability when the character is in certain states
- **`AbilityAuthorized`**: A single bool that checks blocking states + `AbilityPermitted` in one call
- **Animator integration**: `RegisterAnimatorParameter()`, `UpdateAnimator()`
- **Pre-wired references**: `_character`, `_health`, `_movement`, `_condition`, `_animator`, `_inputManager` — all cached by `base.Initialization()`

---

### Lines 28-33: HelpBox

```csharp
public override string HelpBoxText() =>
    "Manages a ring of orbiting swords around the player. " + ...
```
TDE convention. `CharacterAbility` has a virtual `HelpBoxText()`. If overridden, TDE's custom Inspector draws an info box at the top of this component's Inspector panel with this text. Purely for editor UX — helps designers understand what the component does without reading the code.

---

### Lines 37-38: OrbitState Enum

```csharp
public enum OrbitState { Idle, Spawning, Sweeping, Orbiting }
```
A simple state machine with 4 states:
- **Idle** — no swords exist, nothing happening
- **Spawning** — `UpdateWeapons()` was called, swords are being fetched from the pool
- **Sweeping** — swords are animating from their spawn angle to their target positions
- **Orbiting** — all swords have arrived, the pivot is rotating, immunity is active

The state only transitions forward: `Idle → Spawning → Sweeping → Orbiting`. It can jump back to `Spawning` when `UpdateWeapons()` is called again (e.g., pickup, respawn). `ChangeOrbitState()` enforces the transitions and stops rotation when entering `Idle` or `Spawning`.

---

### Lines 57-69: Weapon Setup Fields

```csharp
public GameObject WeaponPrefab;
```
The sword prefab (`OrbitSword.prefab`). Assigned once in the Inspector. All swords in the game are instances of this one prefab — differentiated at runtime by `WeaponDefinition`. If null at `Initialization()`, `ResolveWeaponPrefab()` tries to load it from the asset database (editor) or Resources folder (build). This is the prefab the shared pool creates instances of.

```csharp
public OrbitWeaponDefinition WeaponDefinition;
```
A `ScriptableObject` that defines this character's sword type: sprite, MaxHealth, ClashDamage, EntityDamage, cooldowns. Each enemy prefab gets a different definition assigned (e.g., `Sword_Level_3` for level-3 enemies). When the player picks up loot, `AddWeapons()` replaces this with the enemy's definition — upgrading the player's sword level. Null means "use the prefab's defaults" (no override).

```csharp
public int WeaponCount = 3;
```
How many swords this character starts with and respawns with. This is both an initial value AND a runtime value — `UpdateWeapons()` overwrites it whenever the sword count changes. After picking up 3 swords with 8 active, this becomes 11. On respawn, `OnRespawn()` calls `UpdateWeapons(WeaponCount)` which uses whatever value is currently stored — but since `ResetAbility()` runs first and the prefab's serialized value is 3, it effectively resets.

---

### Lines 73-80: Pooling Fields

```csharp
public int PoolSize = 20;
```
How many swords to pre-create in the shared pool at scene load. Only the **first** `CharacterWeaponsOrbit` instance with a given `WeaponPrefab` creates the pool — all subsequent instances reuse it. Set this high enough to cover peak simultaneous usage: (player swords) + (max active enemies × their sword count). If all 20 are active and more are needed, `PoolCanExpand = true` auto-creates new ones at the cost of one `Instantiate()` call + GC allocation.

---

### Lines 84-108: Orbit Motion and Spawn Sweep Fields

```csharp
public float OrbitRadius = 1.5f;
```
Distance in world units from the character's center to each sword. Used in `OrbitPosition()` to calculate the XY position on the orbit circle: `x = radius * cos(angle)`, `y = radius * sin(angle)`. Higher values = wider ring. Must be > 0.1 (clamped by `OnValidate()`).

```csharp
public float OrbitSpeed = 180f;
```
Degrees per second the pivot rotates. At 180, the ring completes a full rotation in 2 seconds. Applied every frame in `PerformOrbit()`: `_pivot.Rotate(0, 0, OrbitSpeed * Time.deltaTime)`. Positive = counter-clockwise. Negative = clockwise. Zero = no rotation (swords stay fixed).

```csharp
public float SpawnAngleOffset = -45f;
```
The angle (in degrees) where ALL swords initially appear before sweeping to their target positions. -45 degrees = bottom-right of the circle. All swords start stacked at this single point, then fan out to their evenly-spaced positions. Creates a "burst" visual on spawn.

```csharp
public float ArrivalDuration = 0.5f;
```
How long (seconds) each sword takes to sweep from `SpawnAngleOffset` to its target position. The sweep uses `SweepCurve` for easing. 0.5 = half a second. Clamped to minimum 0.01 by `OnValidate()` to prevent divide-by-zero in the sweep calculation.

```csharp
public AnimationCurve SweepCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
```
An easing curve for the sweep animation. X axis = time (0 to 1), Y axis = progress (0 to 1). Linear by default (constant speed). To make swords ease in/out, edit the curve in the Inspector. The curve is evaluated every frame in `SweepWeaponToPosition()`: `progress = SweepCurve.Evaluate(t)` where `t = elapsed / ArrivalDuration`.

---

### Lines 112-136: Rendering and Pick Conditions Fields

```csharp
public int WeaponSortingOrder = 100;
```
Sorting order for sword SpriteRenderers. Higher values render on top. 100 puts swords above ground tiles (order 0), enemies (order ~10-50), and most other sprites. Applied by `ApplyWeaponSortingOrder()` every time a sword is fetched from the pool — because pool reuse means a sword might have a different sorting order from its previous owner.

```csharp
public float WeaponRotationOffset = -45f;
```
Degrees added to each sword's Z rotation so the blade tip points outward (away from the character center). Without this offset, the sword sprite would face its default orientation regardless of orbit position. -45 works for sprites where the tip naturally points up-right at 0 rotation. Adjust per sprite.

```csharp
public bool RequireCharacterComponent = true;
public bool RequirePlayerType = true;
```
Guard conditions (TDE convention). Used by pick-condition systems to verify who can interact with this character's orbit system. `RequireCharacterComponent` = only GameObjects with a `Character` component. `RequirePlayerType` = only `CharacterTypes.Player`, not AI. The `[MMCondition]` attribute hides `RequirePlayerType` in the Inspector unless `RequireCharacterComponent` is true.

---

### Lines 140-144: Animator Parameters

```csharp
protected const string _orbitingAnimationParameterName = "Orbiting";
protected int _orbitingAnimationParameter;
```
TDE convention: store the parameter name as a `const string` and its hashed int separately. String lookups on Animator are slow (hashing every frame). `RegisterAnimatorParameter()` in `InitializeAnimatorParameters()` hashes the string ONCE and stores the result in `_orbitingAnimationParameter`. Then `UpdateAnimator()` uses the cached hash every frame — O(1) instead of O(n) string comparison.

---

### Lines 148-151: Events

```csharp
public event Action<GameObject> OnWeaponDestroyed;
```
Fired when a sword is destroyed in combat (health reaches 0). Passes the destroyed sword's `GameObject`. External scripts can subscribe: `orbit.OnWeaponDestroyed += MyHandler;`. Used for VFX, sound, score, or UI updates when a sword is lost. Fired by `HandleWeaponDestroyed()` AFTER the sword is removed from `_activeWeapons` and reparented to the pool.

```csharp
public event Action OnSweepComplete;
```
Fired when ALL swords have finished their sweep animation and the ring enters Orbiting state. Useful for triggering effects that should happen once the full ring is in position (e.g., immunity VFX, audio). Fired by `OnWeaponArrived()` when `_weaponsArrived == WeaponCount`.

---

### Lines 155-173: Public Read-Only Properties

```csharp
public OrbitState CurrentOrbitState => _orbitState;
```
Exposes the private `_orbitState` as read-only. External scripts can check `orbit.CurrentOrbitState == OrbitState.Orbiting` without being able to change it directly. Arrow syntax `=>` means this is computed every access (no caching), but `_orbitState` is just an enum (4 bytes), so it's trivially cheap.

```csharp
public bool IsOrbiting => _isRotating;
```
Whether the pivot is currently rotating. True only after all swords arrive and `StartOrbit()` is called. False when dead, spawning, or no swords. Simpler check than `CurrentOrbitState == Orbiting` for code that just needs a bool.

```csharp
public int ActiveWeaponCount => _activeWeapons.Count;
```
Number of currently active (visible, orbiting) swords. Decreases when swords are destroyed in combat. Used by `LootExtended` — but by death time this is always 0 (swords die before the enemy), which is why `LootExtended` reads `WeaponCount` (inspector value) instead.

```csharp
public IReadOnlyList<GameObject> Weapons { get { ... } }
```
Returns a read-only list of active sword GameObjects. The trick: it reuses `_weaponsReadOnlyBuffer` (a pre-allocated `List<GameObject>`) every call — `Clear()` + `Add()` loop. This means zero GC allocation even if called every frame. The caller gets `IReadOnlyList<T>` so they can't modify the list. Warning: the buffer is shared — if the caller stores the reference and reads it later, the contents may have changed.

---

### Lines 177-180: Constants

```csharp
protected const float MinSweepAngle = 0.5f;
```
If a sword's angular distance (spawn angle to target angle) is less than 0.5 degrees, skip the sweep animation entirely — just snap to position. Prevents a coroutine from running for an imperceptibly small animation. 0.5 degrees is ~0.009 radians — invisible at any orbit radius.

```csharp
protected const string PivotName = "OrbitPivot";
```
The name of the invisible child GameObject that acts as the rotation pivot. Used by `GetOrCreatePivot()` to find an existing pivot (`transform.Find(PivotName)`) or create one. Named as a constant so it's consistent and searchable.

---

### Lines 184-189: WeaponEntry Struct

```csharp
protected struct WeaponEntry
{
    public GameObject        Go;
    public OrbitWeaponCombat Behaviour;
}
```
Pairs a sword's `GameObject` with its `OrbitWeaponCombat` component. Stored in `_activeWeapons` list. Using a struct (not class) means:
- No heap allocation per entry — stored inline in the `List<T>`'s internal array
- No GC pressure when entries are added/removed
- Cached `Behaviour` avoids `GetComponent<OrbitWeaponCombat>()` every time we need to unsubscribe or apply definitions

---

### Lines 191-211: Static Shared Pool

```csharp
public static List<MMSimpleObjectPooler> SwordPoolers = new List<MMSimpleObjectPooler>();
```
**Static** — shared across ALL instances of `CharacterWeaponsOrbit` in the scene. One entry per unique `WeaponPrefab`. If player and 5 enemies all use the same `OrbitSword.prefab`, there's only ONE pooler in this list, and all 6 characters draw from it. This is the exact same pattern `Loot.SimplePoolers` uses. `public` so it could be inspected by debug tools if needed.

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
protected static void InitializeStatics()
{
    SwordPoolers = new List<MMSimpleObjectPooler>();
}
```
Unity attribute that calls this method automatically when entering Play Mode (before any scene loads). `SubsystemRegistration` is the earliest possible timing — runs before `Awake()` on any GameObject. Resets `SwordPoolers` to a fresh empty list so stale references from a previous Play Mode session don't leak in. Without this, entering Play Mode a second time would find destroyed pooler references in the list (Unity destroys scene objects but C# static fields survive domain reload if "Reload Domain" is disabled in Project Settings).

```csharp
protected MMSimpleObjectPooler _swordPooler;
```
This instance's reference to the shared pooler. Cached in `Initialization()` via `FindSwordPooler()`. Used by `GetOrCreateWeapon()` to fetch swords. All enemies with the same `WeaponPrefab` point to the exact same pooler object.

```csharp
protected Transform _waitingPoolTransform;
```
The transform where returned swords should be reparented. This is the `_waitingPool` child inside the pooler — where `FillObjectPool()` originally parents the swords. We cache it in `Initialization()` via `GetComponentInChildren<MMObjectPool>(true)`. Without this, returned swords would be reparented to the pooler's root instead of the waiting pool child, causing hierarchy inconsistency.

---

### Lines 215-226: Instance State

```csharp
protected List<WeaponEntry> _activeWeapons = new List<WeaponEntry>();
```
All swords currently active and visible in THIS character's orbit ring. Each entry is a `WeaponEntry` struct (GameObject + OrbitWeaponCombat). Entries are added in `GetOrCreateWeapon()`, removed in `HandleWeaponDestroyed()`, and bulk-cleared in `ReturnAllWeaponsToPool()`. Count == the number of swords currently orbiting this specific character.

```csharp
protected List<GameObject> _weaponsReadOnlyBuffer = new List<GameObject>();
```
Reusable buffer for the `Weapons` property. Pre-allocated once, cleared and refilled every time `Weapons` is accessed. Avoids allocating a new `List<GameObject>` on every property access. This is a common Unity pattern for zero-GC public collections.

```csharp
protected Transform _pivot;
```
The invisible child Transform that rotates every frame. All swords are children of this pivot, so rotating the pivot rotates all swords together. The character's own transform is never rotated — only the pivot. This prevents the character's sprite from spinning with the swords.

```csharp
protected OrbitState _orbitState = OrbitState.Idle;
```
Current state of the orbit state machine. Starts `Idle`. Managed by `ChangeOrbitState()`. External code reads it via `CurrentOrbitState` property.

```csharp
protected int _weaponsArrived;
```
Counter tracking how many swords have completed their sweep animation. Reset to 0 in `UpdateWeapons()`. Incremented by `OnWeaponArrived()` (called by each sword's sweep coroutine). When `_weaponsArrived == WeaponCount`, all swords are in position → transition to `Orbiting` state, start rotation, grant immunity.

```csharp
protected bool _isRotating;
```
Whether the pivot should rotate this frame. Set `true` by `StartOrbit()`, `false` by `StopOrbit()`, `OnDeath()`, and `ReturnAllWeaponsToPool()`. Checked every frame in `PerformOrbit()` — if false, `_pivot.Rotate()` is skipped. Also used by `UpdateAnimator()` to sync the Animator's "Orbiting" parameter.

---

### Lines 235-255: Initialization()

```csharp
protected override void Initialization()
{
    base.Initialization();
```
Called once by TDE's `Character.Start()`. `base.Initialization()` (from `CharacterAbility`) does critical wiring:
- Caches `_character` = `GetComponent<Character>()`
- Caches `_health` = `_character.Health` (the Health component)
- Caches `_movement` = `_character._movement` (CharacterMovement state)
- Caches `_condition` = `_character._condition` (CharacterConditions state)
- Caches `_animator` = `_character._animator`
- Caches `_inputManager` = `_character.LinkedInputManager`
- Subscribes to `_health.OnDeath`, `_health.OnHit`, etc.
ALL of these are null until `base.Initialization()` runs. Calling any of them before this line = NullReferenceException.

```csharp
    ResolveWeaponPrefab();
```
If `WeaponPrefab` is null (not assigned in Inspector), tries to find `OrbitSword.prefab` via `AssetDatabase` (editor) or `Resources.Load` (build). Safety net so the system doesn't break if someone forgets to assign the prefab.

```csharp
    if (WeaponPrefab == null)
    {
        Debug.LogError("[CharacterWeaponsOrbit] WeaponPrefab is null. ...", this);
        return;
    }
```
Hard stop if no prefab found. The `this` parameter in `Debug.LogError` makes the error clickable in the Console — clicking it highlights this component in the Inspector. `return` prevents the rest of `Initialization()` from running with a null prefab, which would cause `NullReferenceException` in `FindSwordPooler()`.

```csharp
    _swordPooler = FindSwordPooler();
```
Finds or creates the shared pool for this `WeaponPrefab`. If another character already created a pool for the same prefab, returns that same pool. Otherwise creates a new `MMSimpleObjectPooler`, fills it with `PoolSize` swords, and registers it in `SwordPoolers[]`.

```csharp
    var objectPool = _swordPooler.GetComponentInChildren<MMObjectPool>(true);
    _waitingPoolTransform = objectPool != null ? objectPool.transform : _swordPooler.transform;
```
`MMSimpleObjectPooler.FillObjectPool()` creates a child called `[SimpleObjectPooler] OrbitSword` and attaches an `MMObjectPool` component to it. All initial swords are parented under THIS child (the "waiting pool"). We cache its transform so when swords are returned to the pool later, they go back to the same parent — keeping the hierarchy consistent. The `(true)` parameter means "search inactive children too". Fallback to `_swordPooler.transform` if the waiting pool somehow doesn't exist.

```csharp
    _pivot = GetOrCreatePivot();
```
Finds a child named "OrbitPivot" or creates one. The pivot is an empty `GameObject` — no renderer, no collider, just a `Transform`. It's positioned at the character's center (`localPosition = Vector3.zero`). All swords are parented to this pivot, so rotating it rotates all swords.

```csharp
    UpdateWeapons(WeaponCount);
```
Spawns the initial ring of swords. Calls `ReturnAllWeaponsToPool()` (no-op on first call — no active swords), then `SpawnAndAnimateWeapons()`.

---

### Lines 264-268: ProcessAbility()

```csharp
public override void ProcessAbility()
{
    base.ProcessAbility();
    PerformOrbit();
}
```
Called every frame by `Character.Update()` → `Character.EarlyProcessAbility()` → `Character.ProcessAbility()` → each ability's `ProcessAbility()`. TDE calls this instead of `Update()` so the Character component controls the execution order of all abilities. `base.ProcessAbility()` is empty in `CharacterAbility` but must be called for future-proofing (TDE may add logic there). `PerformOrbit()` does the actual pivot rotation.

---

### Lines 274-277: HandleInput()

```csharp
protected override void HandleInput()
{
    // Intentionally empty — orbit runs passively.
}
```
TDE calls `HandleInput()` every frame before `ProcessAbility()`. Abilities like shooting or dashing read button presses here. Orbit has no input — it runs passively. Overridden and left empty to document the intentional absence of input handling. Without this override, the base class's empty `HandleInput()` runs — same result, but less clear intent.

---

### Lines 284-290: InitializeAnimatorParameters()

```csharp
protected override void InitializeAnimatorParameters()
{
    RegisterAnimatorParameter(
        _orbitingAnimationParameterName,
        AnimatorControllerParameterType.Bool,
        out _orbitingAnimationParameter);
}
```
Called once during `base.Initialization()`. `RegisterAnimatorParameter()` does two things:
1. Checks if the Animator has a parameter named "Orbiting" — if not, silently skips (no error)
2. If found, hashes the string to an int and stores it in `_orbitingAnimationParameter`
From this point on, `UpdateAnimator()` uses the hashed int to set the parameter — O(1) instead of string comparison. This is TDE's standard pattern for all animator integration.

---

### Lines 296-303: UpdateAnimator()

```csharp
public override void UpdateAnimator()
{
    MMAnimatorExtensions.UpdateAnimatorBool(
        _animator,
        _orbitingAnimationParameter,
        (_isRotating && _activeWeapons.Count > 0),
        _character._animatorParameters);
}
```
Called by TDE after `ProcessAbility()` every frame. Sets the Animator's "Orbiting" bool to `true` only when the pivot is rotating AND there's at least one sword. `_character._animatorParameters` is a cached `HashSet<int>` of all parameter hashes that actually exist on the Animator — `UpdateAnimatorBool` checks this set before calling `Animator.SetBool()`, avoiding warnings for missing parameters.

---

### Lines 309-314: OnDeath()

```csharp
protected override void OnDeath()
{
    base.OnDeath();
    _isRotating = false;
    SetImmunity(false);
}
```
Called by TDE when `Health.Kill()` fires `OnDeath` event. `base.OnDeath()` stops ability feedbacks/SFX. We stop the orbit rotation and remove immunity. Note: we do NOT return swords to pool here — `ResetAbility()` handles that separately (called by TDE during the death→respawn transition). This separation matches TDE's own design: `OnDeath` = react to the event, `ResetAbility` = prepare for next life.

---

### Lines 325-333: OnRespawn()

```csharp
protected override void OnRespawn()
{
    base.OnRespawn();
    UpdateWeapons(WeaponCount);
}
```
Called by TDE when the character respawns. `ResetAbility()` already returned all swords to the pool. Now we rebuild the ring with `WeaponCount` swords. `WeaponCount` was overwritten during gameplay (pickups, etc.) but since `UpdateWeapons()` also writes to it, the respawn uses whatever the last value was. For enemies, this is the prefab's serialized value (enemies don't pick up weapons). For the player, this could be the upgraded count from last life — depending on whether `ResetAbility` resets it.

---

### Lines 340-344: ResetAbility()

```csharp
public override void ResetAbility()
{
    base.ResetAbility();
    ReturnAllWeaponsToPool();
}
```
Called by TDE during death→respawn transition, BEFORE `OnRespawn()`. `base.ResetAbility()` resets internal ability state. We return all swords to the shared pool so they're available for reuse. This is the clean separation: `ResetAbility` = cleanup, `OnRespawn` = rebuild.

---

### Lines 351-357: OnDestroy()

```csharp
protected virtual void OnDestroy()
{
    ReturnAllWeaponsToPool();
}
```
Unity lifecycle: called when this `GameObject` is about to be destroyed (scene unload, explicit `Destroy()`, or application quit). Critical for the shared pool: without this, swords parented to our pivot are cascade-destroyed with us, leaving the pooler's tracked list with null references → `MissingReferenceException` on the next `GetPooledGameObject()` call. `ReturnAllWeaponsToPool()` deactivates swords and reparents them to `_waitingPoolTransform` (the pooler's child), saving them from cascade destruction.

---

### Lines 369-374: AddWeapons()

```csharp
public virtual void AddWeapons(int count, OrbitWeaponDefinition definition = null)
{
    if (count <= 0) return;
    if (definition != null) WeaponDefinition = definition;
    UpdateWeapons(_activeWeapons.Count + count);
}
```
Public API called by `PickableItemExtended.Pick()` when the player collects a weapon drop.

- `count <= 0` guard: no-op for zero or negative values. Prevents removing swords via `AddWeapons(-5)`.
- `definition != null` check: if the pickup carries a definition (it always does for enemy drops), replace the player's `WeaponDefinition`. This is how weapon level upgrades work — pick up a Level 3 drop → all your swords become Level 3.
- `_activeWeapons.Count + count`: adds to the CURRENT active count, not the Inspector's `WeaponCount`. So if the player had 8 swords and 2 were destroyed in combat (6 active), picking up 3 gives `6 + 3 = 9`, not `8 + 3 = 11`.
- `UpdateWeapons()` returns all current swords to pool and respawns the new total — all with the updated `WeaponDefinition`.

---

### Lines 381-393: UpdateWeapons()

```csharp
public virtual void UpdateWeapons(int newWeaponCount)
{
    StopAllCoroutines();
```
Cancels any in-progress sweep coroutines from a previous spawn. Without this, old sweep coroutines would keep running and move swords that have already been returned to the pool — causing `NullReferenceException` or visual glitches.

```csharp
    WeaponCount     = Mathf.Max(0, newWeaponCount);
    _weaponsArrived = 0;
```
`Mathf.Max(0, ...)` prevents negative sword counts. `_weaponsArrived` reset to 0 so the sweep completion logic starts fresh. Each sword's sweep coroutine will increment this when it finishes.

```csharp
    ReturnAllWeaponsToPool();
    ChangeOrbitState(OrbitState.Spawning);
```
Return ALL current swords to the pool (deactivate, reparent, unsubscribe events). Then transition to `Spawning` state — `ChangeOrbitState` also calls `StopOrbit()` when entering Spawning.

```csharp
    if (WeaponCount > 0)
        SpawnAndAnimateWeapons();
}
```
Edge case: `WeaponCount == 0` means "clear all swords". No spawn, no sweep, `OnWeaponArrived` never fires, immunity stays false. The character is vulnerable. This is intentional — it's how the last sword being destroyed works.

---

### Lines 396-399: StartOrbit() / StopOrbit()

```csharp
public virtual void StartOrbit() => _isRotating = true;
public virtual void StopOrbit()  => _isRotating = false;
```
Simple bool setters. Separated into methods so external code can call them (e.g., pause orbit during a cutscene) and so the logic has a named entry point instead of raw field access. `virtual` so subclasses can add effects (e.g., play a start/stop sound).

---

### Lines 410-425: PerformOrbit()

```csharp
protected virtual void PerformOrbit()
{
    if (!AbilityAuthorized)
        return;
```
`AbilityAuthorized` (from `CharacterAbility`) is a single bool that evaluates:
1. Is `AbilityPermitted` true? (general on/off toggle)
2. Is the character's movement state NOT in `BlockingMovementStates[]`?
3. Is the character's condition state NOT in `BlockingConditionStates[]`?
If ANY check fails, the ability is blocked. You configure blocking in the Inspector — add `Dashing` to `BlockingMovementStates` and orbit pauses while dashing. Zero code changes needed.

```csharp
    if (_condition.CurrentState != CharacterStates.CharacterConditions.Normal)
        return;
```
Secondary guard — the character must be in `Normal` condition. This catches states like `Dead`, `Frozen`, `Stunned` that might not be in the blocking list. Mirrors the TDE ability template exactly.

```csharp
    if (_isRotating && _pivot != null && _activeWeapons.Count > 0)
        _pivot.Rotate(0f, 0f, OrbitSpeed * Time.deltaTime, Space.Self);
}
```
The actual rotation. Only happens when:
- `_isRotating` is true (set by `StartOrbit()` after all swords arrive)
- `_pivot` exists (always should, but null-safe)
- At least one sword is active (no point rotating an empty ring)

`Space.Self` means rotate around the pivot's own Z axis (local space), not world Z. Since the pivot is always at identity rotation relative to the character, this is equivalent to world Z. The `0, 0, degrees` parameters mean: no X rotation, no Y rotation, `OrbitSpeed * deltaTime` degrees around Z. `deltaTime` makes it framerate-independent.

---

### Lines 433-448: ReturnAllWeaponsToPool()

```csharp
protected virtual void ReturnAllWeaponsToPool()
{
    foreach (var entry in _activeWeapons)
    {
        if (entry.Go == null) continue;
```
Skip destroyed swords. A sword might have been destroyed by Unity between frames (scene unload edge case). Accessing a destroyed GameObject throws `MissingReferenceException`.

```csharp
        UnsubscribeWeapon(entry.Behaviour);
```
`wb.OnDestroyed -= HandleWeaponDestroyed`. MUST happen before deactivation — otherwise, `SetActive(false)` might trigger `OnDisable` on `OrbitWeaponCombat` which might fire `OnDestroyed`, calling `HandleWeaponDestroyed` while we're iterating `_activeWeapons` → collection modified during iteration → crash.

```csharp
        entry.Go.SetActive(false);
```
Deactivates the sword. `MMSimpleObjectPooler.GetPooledGameObject()` finds objects by checking `!activeInHierarchy`, so this makes the sword "available" in the pool.

```csharp
        if (_waitingPoolTransform != null)
            entry.Go.transform.SetParent(_waitingPoolTransform);
    }
```
Reparent from the character's pivot to the pooler's waiting pool child. This is critical: if the character is later destroyed (`Destroy(enemyGO)`), cascade destruction would kill all children — including swords still parented to the pivot. By reparenting to the pooler (which is a static scene object), swords survive character destruction.

```csharp
    _activeWeapons.Clear();
    _isRotating = false;
    SetImmunity(false);
}
```
Clear the active list (all swords are now in the pool). Stop rotation (nothing to rotate). Remove immunity (no shield).

---

### Lines 456-474: SpawnAndAnimateWeapons()

```csharp
protected virtual void SpawnAndAnimateWeapons()
{
    if (WeaponCount <= 0 || _pivot == null) return;

    float angleStep = 360f / WeaponCount;
```
Divide 360 degrees evenly among all swords. 3 swords → 120 degrees apart. 5 swords → 72 degrees apart. This is the angular spacing between each sword's final position.

```csharp
    for (int i = 0; i < WeaponCount; i++)
    {
        float targetAngle = i * angleStep;
```
Sword 0 → 0 degrees (right). Sword 1 → 120 degrees (upper-left for 3 swords). Sword 2 → 240 degrees (lower-left). Evenly spaced around the circle.

```csharp
        var weapon = GetOrCreateWeapon(SpawnAngleOffset, targetAngle);
        if (weapon == null) continue;
```
Fetch a sword from the pool, parent it to the pivot, position it at `SpawnAngleOffset` (the initial clustered position), configure it. `null` guard in case the pool somehow fails.

```csharp
        float angularDistance = targetAngle - SpawnAngleOffset;
        if (angularDistance < 0f) angularDistance += 360f;
```
How far (in degrees) this sword needs to sweep from spawn to its target. The `+= 360f` handles wrap-around: if spawn is at 350 degrees and target is at 10 degrees, raw difference is -340, corrected to +20.

```csharp
        StartCoroutine(SweepWeaponToPosition(
            weapon, SpawnAngleOffset, targetAngle, angularDistance));
    }
    ChangeOrbitState(OrbitState.Sweeping);
}
```
Start a separate coroutine for each sword — they all animate simultaneously. After launching all coroutines, transition to `Sweeping` state.

---

### Lines 480-524: GetOrCreateWeapon()

```csharp
GameObject weapon = _swordPooler.GetPooledGameObject();
```
Fetches an inactive sword from the shared pool. `GetPooledGameObject()` scans `_objectPool.PooledGameObjects` for the first inactive object. If none found and `PoolCanExpand = true` (our setting), creates a new one via `Instantiate()`. Returns null only if `PoolCanExpand = false` and all swords are active.

```csharp
weapon.transform.SetParent(_pivot, false);
```
Parent the sword to this character's pivot. `false` = `worldPositionStays = false`, meaning the sword's local transform is preserved relative to the new parent. Without this, Unity would recalculate the local position to maintain the same world position, which would place the sword far from the character.

```csharp
weapon.transform.localPosition = OrbitPosition(spawnAngle);
weapon.transform.localRotation = Quaternion.Euler(0f, 0f, spawnAngle + WeaponRotationOffset);
```
Position the sword on the orbit circle at `spawnAngle` (usually `SpawnAngleOffset` = -45 degrees). All swords start at the same angle before sweeping to their individual targets. The rotation offset makes the blade tip point outward.

```csharp
weapon.tag = ResolveOwnerTag();
```
Sets the sword's tag to "Player" or "Enemy" based on the owner's `CharacterType`. Used by the damage system to determine friendly fire — swords tagged "Player" don't damage the player, swords tagged "Enemy" don't damage enemies.

```csharp
EnsureKinematicRigidbody(weapon);
ApplyWeaponSortingOrder(weapon);
```
`EnsureKinematicRigidbody`: adds or configures a `Rigidbody2D` as Kinematic with zero gravity. Prevents physics from flinging the sword when the character moves. Must be Kinematic because the sword's position is controlled by code (orbit), not physics.

`ApplyWeaponSortingOrder`: sets `SpriteRenderer.sortingOrder` to `WeaponSortingOrder` (default 100). This runs every time because pool reuse means a sword might have a different sorting order from its previous owner.

```csharp
weapon.SetActive(true);
```
Activates the sword. On first activation (fresh from pool), this triggers `Awake()` and `Start()` on all components — including `OrbitWeaponCombat`'s initialization that caches `SpriteRenderer`, `Health`, etc. MUST happen BEFORE `ApplyWeaponDefinition` because `ApplyDefinition` writes to those cached references. If called before `SetActive(true)`, the cached refs are null and `ApplyDefinition` silently fails.

```csharp
var wb = weapon.GetComponent<OrbitWeaponCombat>();
if (wb != null)
{
    ApplyWeaponDefinition(wb);
    wb.OnDestroyed += HandleWeaponDestroyed;
}
```
Get the combat component, apply the current `WeaponDefinition` (sprite + stats), and subscribe to its destruction event. `GetComponent` is called after `SetActive(true)` so `Awake()` has already run. The `+= HandleWeaponDestroyed` subscription means: when this sword's health reaches 0 and `OrbitWeaponCombat` fires `OnDestroyed`, our `HandleWeaponDestroyed` method will be called.

```csharp
_activeWeapons.Add(new WeaponEntry { Go = weapon, Behaviour = wb });
return weapon;
```
Register the sword in our active list and return it to `SpawnAndAnimateWeapons()` for sweep animation.

---

### Lines 530-537: OrbitPosition()

```csharp
protected virtual Vector3 OrbitPosition(float degrees)
{
    float rad = degrees * Mathf.Deg2Rad;
    return new Vector3(
        OrbitRadius * Mathf.Cos(rad),
        OrbitRadius * Mathf.Sin(rad),
        0f);
}
```
Unit circle math. Converts an angle in degrees to an XY position on the orbit circle. `Mathf.Deg2Rad` = `π / 180`. At 0 degrees → `(radius, 0)` = right. At 90 degrees → `(0, radius)` = top. Z is always 0 (2D game). This is the standard parametric circle equation: `x = r·cos(θ)`, `y = r·sin(θ)`.

---

### Lines 545-579: SweepWeaponToPosition()

```csharp
protected virtual IEnumerator SweepWeaponToPosition(
    GameObject weapon, float startAngle, float targetAngle, float angularDistance)
{
    if (weapon == null) { OnWeaponArrived(); yield break; }
```
Coroutine that animates one sword from `startAngle` to `targetAngle`. First null check: if the weapon was destroyed or returned to pool between the `StartCoroutine` call and the first frame this coroutine runs, skip animation and count this sword as "arrived".

```csharp
    if (angularDistance < MinSweepAngle)
    {
        SetWeaponTransform(weapon, targetAngle);
        OnWeaponArrived();
        yield break;
    }
```
If the sword barely needs to move (< 0.5 degrees), snap instantly instead of running a multi-frame animation. Prevents a coroutine from running for an imperceptible movement.

```csharp
    float elapsed = 0f;
    while (elapsed < ArrivalDuration)
    {
        if (weapon == null) { OnWeaponArrived(); yield break; }
```
Main animation loop. Runs for `ArrivalDuration` seconds. Null check every frame in case the sword is destroyed mid-sweep (e.g., enemy kills the player during spawn animation).

```csharp
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / ArrivalDuration);
```
`t` goes from 0.0 to 1.0 over `ArrivalDuration` seconds. `Clamp01` ensures it never exceeds 1.0 due to frame time variance.

```csharp
        float currentAngle = startAngle + angularDistance * SweepCurve.Evaluate(t);
        SetWeaponTransform(weapon, currentAngle);
        yield return null;
    }
```
`SweepCurve.Evaluate(t)` returns 0.0→1.0 with custom easing. Linear curve = constant speed. Ease-in/out curve = slow start, fast middle, slow end. The result is multiplied by `angularDistance` to get the current angle offset from `startAngle`. `yield return null` pauses the coroutine until the next frame.

```csharp
    if (weapon != null)
        SetWeaponTransform(weapon, targetAngle);
    OnWeaponArrived();
}
```
After the loop, snap to the exact `targetAngle` to eliminate floating-point drift. Then notify the system this sword has arrived.

---

### Lines 585-599: OnWeaponArrived()

```csharp
protected virtual void OnWeaponArrived()
{
    _weaponsArrived++;
    if (_weaponsArrived < WeaponCount) return;
```
Increment the arrival counter. If not all swords have arrived yet, return — nothing to do. This is a "barrier" pattern: all N coroutines run independently, but the state transition only happens when ALL of them are done.

```csharp
    OnSweepComplete?.Invoke();
    ChangeOrbitState(OrbitState.Orbiting);
    StartOrbit();
    SetImmunity(true);
}
```
All swords are in position:
1. Fire the `OnSweepComplete` event for external listeners
2. Transition to `Orbiting` state
3. Start pivot rotation
4. Grant damage immunity — `ImmuneToDamage` is used instead of `Invulnerable` to avoid conflicting with TDE's own post-hit invincibility coroutine

---

### Lines 607-632: HandleWeaponDestroyed()

```csharp
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
```
Called when `OrbitWeaponCombat.OnDestroyed` fires (sword health reached 0). Linear search through `_activeWeapons` to find the matching entry. Unsubscribe the event handler first (prevent double-calls), then remove from the active list. `break` after first match — each sword appears only once. `RemoveAt(i)` shifts all subsequent entries left — O(n) but the list is tiny (3-10 swords).

```csharp
    if (_waitingPoolTransform != null)
        weapon.transform.SetParent(_waitingPoolTransform);
```
Reparent the destroyed sword to the pooler's waiting pool BEFORE `MMPoolableObject.Destroy()` deactivates it. This prevents cascade destruction: if the character is later destroyed, the sword is safe under the pooler. `MMPoolableObject.Destroy()` will call `SetActive(false)` after our handler returns, making the sword available for pool reuse.

```csharp
    OnWeaponDestroyed?.Invoke(weapon);

    if (_activeWeapons.Count == 0)
        SetImmunity(false);
}
```
Propagate the event upward for external listeners (VFX, UI, score). If no swords remain, remove immunity — the character is now vulnerable.

---

### Lines 641-648: ChangeOrbitState()

```csharp
protected virtual void ChangeOrbitState(OrbitState newState)
{
    if (_orbitState == newState) return;
    _orbitState = newState;

    if (newState == OrbitState.Idle || newState == OrbitState.Spawning)
        StopOrbit();
}
```
State machine transition. No-op if already in the target state (prevents redundant side effects). When entering `Idle` or `Spawning`, stops rotation — swords shouldn't spin while they're being set up or when there are none.

---

### Lines 657-667: GetOrCreatePivot()

```csharp
protected virtual Transform GetOrCreatePivot()
{
    Transform existing = transform.Find(PivotName);
    if (existing != null) return existing;
```
`transform.Find("OrbitPivot")` searches direct children only (not recursive). If found, reuse it — this survives domain reloads and recompiles in the editor. The pivot's child swords are preserved.

```csharp
    var go = new GameObject(PivotName);
    go.transform.SetParent(transform, false);
    go.transform.localPosition = Vector3.zero;
    go.transform.localRotation = Quaternion.identity;
    return go.transform;
}
```
If not found, create a new empty GameObject named "OrbitPivot", parent it to the character at origin with no rotation. This is the invisible pivot that rotates every frame.

---

### Lines 678-697: FindSwordPooler()

```csharp
protected virtual MMSimpleObjectPooler FindSwordPooler()
{
    foreach (var pooler in SwordPoolers)
    {
        if (pooler != null && pooler.GameObjectToPool == WeaponPrefab)
            return pooler;
    }
```
Scan the static registry for an existing pooler that uses the same `WeaponPrefab`. The `!= null` check handles stale references from destroyed poolers (scene unload edge case). If found, return it — all characters with the same prefab share one pool.

```csharp
    var newGO     = new GameObject("[MMSimpleObjectPooler] " + WeaponPrefab.name);
    var newPooler = newGO.AddComponent<MMSimpleObjectPooler>();
    newPooler.GameObjectToPool = WeaponPrefab;
    newPooler.PoolSize         = PoolSize;
    newPooler.PoolCanExpand    = true;
    newPooler.NestWaitingPool  = true;
    newPooler.NestUnderThis    = true;
```
No existing pooler found — create one. `NestWaitingPool = true` means pooled swords are parented under the waiting pool child (keeps hierarchy clean). `NestUnderThis = true` means the waiting pool child is parented under the pooler GameObject. `PoolCanExpand = true` means the pool auto-grows if all swords are active (creates new ones via `Instantiate()`).

```csharp
    newPooler.FillObjectPool();
```
Pre-creates `PoolSize` (default 20) inactive sword instances. Uses `MMInstantiateDisabled` — creates swords without triggering `Awake/Start/OnEnable`, so they exist as invisible, dormant objects until `SetActive(true)` is called.

```csharp
    newPooler.Owner = SwordPoolers;
    SwordPoolers.Add(newPooler);
    return newPooler;
}
```
`Owner` is an `MMSimpleObjectPooler` field that stores a back-reference to the list it belongs to. Used internally by the pooler for cleanup. Then register the new pooler in the static list and return it.

---

### Lines 703-713: ResolveWeaponPrefab()

```csharp
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
```
Fallback if `WeaponPrefab` isn't assigned in the Inspector. In the editor, uses `AssetDatabase` (can load any asset by path). In builds, uses `Resources.Load` (requires the prefab to be in a `Resources/` folder). The `#if UNITY_EDITOR` directive excludes editor-only code from builds. This is a safety net — the prefab should always be assigned in the Inspector.

---

### Lines 719-725: ResolveOwnerTag()

```csharp
protected virtual string ResolveOwnerTag()
{
    return (_character.CharacterType ==
            MoreMountains.TopDownEngine.Character.CharacterTypes.Player)
        ? "Player"
        : "Enemy";
}
```
Returns "Player" or "Enemy" based on the character's TDE type. Set on each sword via `weapon.tag = ResolveOwnerTag()`. TDE's damage system uses tags for faction checks — prevents friendly fire. The fully-qualified `MoreMountains.TopDownEngine.Character.CharacterTypes.Player` is needed because our namespace is different.

---

### Lines 731-737: ApplyWeaponDefinition()

```csharp
protected virtual void ApplyWeaponDefinition(OrbitWeaponCombat wb)
{
    if (WeaponDefinition != null)
        wb.ApplyDefinition(WeaponDefinition);
    else
        wb.ResetHealth();
}
```
Central point for applying weapon data to a sword. If a `WeaponDefinition` ScriptableObject is assigned, `ApplyDefinition()` sets: sprite, MaxHealth, ClashDamage, EntityDamage, ClashCooldown, EntityHitCooldown, then resets health to the new MaxHealth. If no definition (null), just reset health to the prefab's default MaxHealth. `virtual` so subclasses could add additional configuration (tint, scale, particle effects).

---

### Lines 743-750: EnsureKinematicRigidbody()

```csharp
protected virtual void EnsureKinematicRigidbody(GameObject weapon)
{
    Rigidbody2D rb = weapon.GetComponent<Rigidbody2D>();
    if (rb == null) rb = weapon.AddComponent<Rigidbody2D>();
    rb.bodyType     = RigidbodyType2D.Kinematic;
    rb.gravityScale = 0f;
    rb.simulated    = true;
}
```
Ensures the sword has a Kinematic `Rigidbody2D`. Kinematic = position controlled by code, not physics. GravityScale 0 = no falling. Simulated = still participates in trigger collisions (needed for damage detection). `AddComponent` only if missing — won't duplicate. Called every pool reuse because a previous owner might have changed these settings (defensive).

---

### Lines 757-761: ApplyWeaponSortingOrder()

```csharp
protected virtual void ApplyWeaponSortingOrder(GameObject weapon)
{
    var sr = weapon.GetComponentInChildren<SpriteRenderer>();
    if (sr != null) sr.sortingOrder = WeaponSortingOrder;
}
```
Sets the sprite's rendering order. `GetComponentInChildren` searches the sword and all its children for a `SpriteRenderer`. Higher `sortingOrder` = drawn on top. 100 (default) puts swords above most game elements. Called every pool reuse because a previous owner might have used a different sorting order.

---

### Lines 768-772: SetImmunity()

```csharp
protected virtual void SetImmunity(bool immune)
{
    if (_health != null)
        _health.ImmuneToDamage = immune;
}
```
Sets the character's damage immunity flag. `ImmuneToDamage` is TDE's `Health` field — checked in `Health.CanTakeDamageThisFrame()` BEFORE the invulnerability check. We use this instead of `Invulnerable` because TDE's own post-hit invincibility coroutine writes to `Invulnerable` — using the same field would cause conflicts. All immunity changes go through this method so there's one place to update if the mechanism changes.

---

### Lines 778-782: UnsubscribeWeapon()

```csharp
protected virtual void UnsubscribeWeapon(OrbitWeaponCombat wb)
{
    if (wb != null)
        wb.OnDestroyed -= HandleWeaponDestroyed;
}
```
Centralised event unsubscription. Called by both `ReturnAllWeaponsToPool()` and `HandleWeaponDestroyed()`. Without this, if we forget to unsubscribe, a returned sword's `OnDestroyed` event would still call `HandleWeaponDestroyed` on the OLD owner — modifying `_activeWeapons` of a character that no longer owns that sword. The null check prevents `NullReferenceException` if the component was destroyed.

---

### Lines 788-792: SetWeaponTransform()

```csharp
protected virtual void SetWeaponTransform(GameObject weapon, float angle)
{
    weapon.transform.localPosition = OrbitPosition(angle);
    weapon.transform.localRotation = Quaternion.Euler(0f, 0f, angle + WeaponRotationOffset);
}
```
Sets a sword's position and rotation for a given orbit angle. Used by both `GetOrCreateWeapon()` (initial placement) and `SweepWeaponToPosition()` (every animation frame). Single point of change — if the rotation formula ever changes, only this method needs updating. `localPosition` and `localRotation` are relative to the parent (the pivot).

---

### Lines 800-806: OnValidate()

```csharp
protected virtual void OnValidate()
{
    OrbitRadius     = Mathf.Max(0.1f,  OrbitRadius);
    WeaponCount     = Mathf.Max(0,      WeaponCount);
    OrbitSpeed      = Mathf.Max(0f,     OrbitSpeed);
    ArrivalDuration = Mathf.Max(0.01f,  ArrivalDuration);
}
```
Unity editor callback — called every time a value is changed in the Inspector. Clamps fields to valid ranges:
- `OrbitRadius` minimum 0.1 — prevents swords from collapsing to the center
- `WeaponCount` minimum 0 — no negative swords
- `OrbitSpeed` minimum 0 — no negative rotation (use negative `WeaponRotationOffset` for clockwise)
- `ArrivalDuration` minimum 0.01 — prevents divide-by-zero in `SweepWeaponToPosition()` where `t = elapsed / ArrivalDuration`

This only runs in the editor — zero runtime cost. `virtual` so subclasses can add their own validation.
