# Enemy Prefab — Setup Guide

**Prefab path:** `Assets/RingOfEldenSwords/Prefabs/Enemies/EnemyPatrolBase.prefab`

---

## Overview

The Enemy prefab is the base for all enemies in Ring of Elden Swords.
It uses Unity **Prefab Variants** — create a variant per enemy type, override only what differs
(`WeaponDefinition`, `WeaponCount`, `Health`). All shared logic lives here and propagates to
every variant automatically.

```
EnemyPatrolBase.prefab                 ← you are here (base)
├── Enemy_Ninja.prefab   (variant)     ← override: sprite, animator, WeaponDefinition
│   ├── Enemy_Ninja_L1.prefab          ← override: WeaponDefinition = Sword_Level_1
│   └── Enemy_Ninja_L5.prefab          ← override: WeaponDefinition = Sword_Level_5
└── Enemy_Samurai.prefab (variant)     ← override: sprite, animator, WeaponDefinition
```

---

## Prefab Hierarchy

```
EnemyPatrolBase (root)
├── [Sprite / Animator / AI components]
├── OrbitPivot                    ← rotation driver for the sword ring (see below)
│   ├── OrbitSword(Clone)
│   ├── OrbitSword(Clone)
│   └── OrbitSword(Clone)
└── OrbitWeaponPickup (inactive)  ← embedded loot drop — born inactive (m_IsActive: 0)
```

The `OrbitWeaponPickup` child is **embedded directly in the enemy prefab** — not instantiated
at runtime. This is intentional and is the key to zero-lag loot drops (see Optimization section).

---

## Components on the Root GameObject

### 1. CharacterOrbitWeapons
**Script:** `Assets/RingOfEldenSwords/Scripts/Characters/CharacterAbilities/CharacterOrbitWeapons.cs`
**Base class:** TDE `CharacterAbility`

Manages the ring of orbiting weapons around this enemy.

| Inspector Field       | Type                   | Description |
|-----------------------|------------------------|-------------|
| **Weapon Prefab**     | GameObject             | The universal orbit sword prefab. Set to `OrbitSword.prefab`. Never change this per variant — it is always the same shell prefab. |
| **Weapon Definition** | OrbitWeaponDefinition  | The ScriptableObject defining this enemy's sword type (sprite, stats). **This is what you change per variant.** |
| **Weapon Count**      | int                    | How many swords orbit this enemy at spawn. |
| **Orbit Radius**      | float                  | Distance from enemy center to each sword (world units). |
| **Orbit Speed**       | float                  | Degrees per second the ring rotates. |
| **Arrival Duration**  | float                  | Seconds each sword takes to sweep into orbit position on spawn. |
| **Spawn Angle Offset**| float                  | Angle (degrees) all swords start from before sweeping outward. |
| **Sweep Curve**       | AnimationCurve         | Easing for the spawn sweep animation. |

**How it works at runtime:**
1. `Initialization()` runs → calls `UpdateWeapons(WeaponCount)`
2. Each sword is spawned/pooled, tagged `"Enemy"`, and `ApplyDefinition(WeaponDefinition)` is called on its `OrbitWeaponCombat`
3. Swords sweep into orbit position using `SweepCurve`
4. Once all swords arrive → `ImmuneToDamage = true` (sword shield active)
5. On death → `ImmuneToDamage = false`, swords removed

---

### OrbitPivot — Required Child GameObject

`CharacterOrbitWeapons` requires a child GameObject named exactly **`OrbitPivot`** on the enemy
(and player) root.

```
Enemy (root)
└── OrbitPivot          ← this GameObject
    ├── OrbitSword(Clone)
    ├── OrbitSword(Clone)
    └── OrbitSword(Clone)
```

**What it does:** The pivot's Z rotation is incremented every frame by `OrbitSpeed`. Swords are
parented to it, so they all rotate together as a ring without the enemy sprite rotating.

**Setup:** `CharacterOrbitWeapons.Initialization()` calls `GetOrCreatePivot()` which:
- Searches for an existing child named `"OrbitPivot"`
- Creates one automatically at `localPosition = Vector3.zero` if not found

> You do not need to create `OrbitPivot` manually. However if you want to see it in the Editor
> hierarchy, add an empty child named `OrbitPivot` — the script will find and reuse it.

**Important:** Do not rename, delete, or reparent `OrbitPivot`. Do not add visual components to it.

---

### 2. EnemyOrbitLoot
**Script:** `Assets/RingOfEldenSwords/Scripts/Enemy/EnemyOrbitLoot.cs`
**Base class:** TDE `TopDownMonoBehaviour`

Handles the weapon pickup dropped when this enemy dies. Works in tandem with the embedded
`OrbitWeaponPickup` child in the prefab.

| Inspector Field    | Type  | Description |
|--------------------|-------|-------------|
| **Scatter Radius** | float | How far (world units) the pickup scatters from the death position. Default: `1.5`. |

**How it works at runtime (see full flow diagram below):**
1. `Start()` → stamps weapon data onto the embedded pickup child
2. `Start()` → starts `PreWarmPickup()` coroutine (see Optimization section)
3. On death → detaches pickup, scatters it, calls `SetActive(true)` — **zero lag**
4. On respawn → re-attaches pickup as inactive child, re-stamps for next death

**Respawn support:**
Call `enemyOrbitLoot.OnRespawn()` from your respawn handler (e.g. `AutoRespawn`, `CheckPoint`)
to reset the pickup for the next death cycle.

---

### 3. OrbitWeaponCombat
**Script:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponCombat.cs`
**Base class:** `MonoBehaviour`
**Lives on:** `OrbitSword.prefab` (the weapon prefab, not the enemy root)

Handles combat for each individual orbiting sword instance.

| Field                 | Type  | Description |
|-----------------------|-------|-------------|
| **Max Health**        | float | HP of this sword. Reaches 0 via clashes → sword destroyed. Overridden by `WeaponDefinition`. |
| **Clash Damage**      | float | Damage dealt to opposing sword on collision. Overridden by `WeaponDefinition`. |
| **Entity Damage**     | int   | Damage dealt to a character's `Health` on contact. Overridden by `WeaponDefinition`. |
| **Clash Cooldown**    | float | Min seconds between clash damage ticks. |
| **Entity Hit Cooldown** | float | Min seconds between entity damage ticks. |

**Faction logic:** sword tag is set to `"Enemy"` by `CharacterOrbitWeapons` at spawn.
Enemy swords only damage `Player`-tagged characters and vice versa.

**Key events:**
- `OnDestroyed` — fired when sword HP reaches 0. `CharacterOrbitWeapons` listens to remove it from the active list.
- `OnClash` — fired when this sword clashes with an opposing sword.

---

## Weapon Definition (ScriptableObject)

**Class:** `OrbitWeaponDefinition`
**Script:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponDefinition.cs`
**Assets:** `Assets/RingOfEldenSwords/ScriptableObjects/Weapons/Swords/`

This is the **single source of truth** for a sword type. Assign one per enemy variant.
Creating a new sword type requires no code and no new prefab — just a new asset.

### How to create a new Weapon Definition
```
Right-click in Project window
→ Create → RingOfEldenSwords → Orbit Weapon Definition
→ Name it (e.g. Sword_Level_5)
→ Fill in the fields below
```

| Field                  | Type   | Description |
|------------------------|--------|-------------|
| **Weapon Name**        | string | Display name used in editor and debug logs. |
| **Sprite**             | Sprite | The sprite shown on orbiting swords and on the loot drop pickup. |
| **Max Health**         | float  | Sword HP before it is destroyed by clashes. |
| **Clash Damage**       | float  | Damage dealt to opposing swords on collision. |
| **Entity Damage**      | int    | Damage dealt to a character on contact. |
| **Clash Cooldown**     | float  | Minimum seconds between clash ticks (default: `0.1`). |
| **Entity Hit Cooldown**| float  | Minimum seconds between entity damage ticks (default: `0.5`). |

### Pre-made Sword Definitions (48 levels)

All 48 levels are in `Assets/RingOfEldenSwords/ScriptableObjects/Weapons/Swords/`.

| Level Range | Stats (Health / Clash / Entity) |
|-------------|----------------------------------|
| Level 1     | 10 / 10 / 10                    |
| Level 2     | 15 / 15 / 15                    |
| Level 5     | 30 / 30 / 30                    |
| Level 10    | 55 / 55 / 55                    |
| Level 20    | 105 / 105 / 105                 |
| Level 48    | 245 / 245 / 245                 |

**Formula:** `Value = 10 + (Level - 1) × 5`

---

## Loot Drop Pickup

**Prefab:** `Assets/RingOfEldenSwords/Prefabs/ItemPickers/OrbitWeaponPickup.prefab`
**Script:** `Assets/RingOfEldenSwords/Scripts/Combat/Pickups/OrbitWeaponPickup.cs`
**Base class:** TDE `PickableItem`

The world item that appears when an enemy dies. Shows the sword sprite and count badge.

| Inspector Field   | Type            | Description |
|-------------------|-----------------|-------------|
| **Weapon Sprite** | SpriteRenderer  | The SpriteRenderer that displays the sword icon. Must be wired in the prefab Inspector. |
| **Count Text**    | TextMeshPro     | TMP component showing `x3` style badge. Auto-wired from children in `Awake()` if not set. |

**Inherited from TDE PickableItem:**

| Field                    | Description |
|--------------------------|-------------|
| **Require Player Type**  | Set to `Player` so only the player can pick this up. |
| **Disable Object On Pick** | Set to `true` so pickup disappears after collection. |

**What happens on pickup:**
1. Player walks over pickup → `PickableItem.OnTriggerEnter2D` fires
2. TDE validates: correct character type, item is pickable
3. `Pick(picker)` is called → finds player's `CharacterOrbitWeapons`
4. Sets `orbit.WeaponDefinition = pickup.WeaponDefinition` — **player now has the enemy's sword type**
5. Calls `orbit.AddWeapons(PickupCount)` — new swords spawn with the correct sprite and stats

---

## Creating a New Enemy Variant (Step by Step)

### Example: Enemy with Level 10 Swords, 6 swords

1. **Right-click** `EnemyPatrolBase.prefab` in Project → **Create → Prefab Variant**
2. Name it `Enemy_Sword_L10`
3. Open `Enemy_Sword_L10` in Prefab Mode
4. Select the root GameObject
5. In **CharacterOrbitWeapons**:
   - Set `Weapon Definition` → drag `Sword_Level_10.asset`
   - Set `Weapon Count` → `6`
6. Save — done

The enemy now spawns with 6 Level-10 swords. When it dies, it drops a pickup showing the
Level-10 sword sprite and count badge `x6`. When the player picks it up, their swords upgrade
to Level-10 type and they gain 6 more swords.

---

## Optimization: Zero-Lag Loot Drops

### Problem: SetActive(true) freeze

The naive implementation instantiates or activates the pickup at enemy death time. This causes a
**visible freeze** (typically 30–80ms) on the frame the enemy dies.

**Root cause:** `SetActive(true)` on a GameObject whose `Start()` has never run triggers Unity
to execute `Start()` synchronously on that same frame. For TDE's `PickableItem`, `Start()` calls
`MMFeedbacks.Initialization()` — an expensive one-time setup that allocates memory, builds
feedback chains, and wires internal event callbacks. This work happens in the middle of the
death frame, blocking the main thread long enough to see a hitch.

This is a well-known Unity issue documented in the Unity forums. Any time you call `SetActive(true)`
on an object that has never been activated, its `Start()` runs immediately — regardless of how
the object was created (Instantiate, pool, embedded child, etc.).

### Why same-frame toggle does not work

An intuitive attempt is to toggle the object on and off in the same frame during `Awake()`:

```csharp
// DOES NOT WORK — Start() is never called
_pickup.gameObject.SetActive(true);
_pickup.gameObject.SetActive(false);
```

Unity queues `Start()` to run at the end of the current frame. If the object is immediately
deactivated in the same frame, Unity cancels the queued `Start()` call — it is never executed.
The expensive initialization is just deferred to the next time `SetActive(true)` is called,
which will be at death time. No benefit.

### The fix: two-frame coroutine pre-warm

`EnemyOrbitLoot.Start()` launches a coroutine that activates the pickup for exactly one frame,
then hides it again:

```csharp
private IEnumerator PreWarmPickup()
{
    _pickup.gameObject.SetActive(true);   // queues Start() for this frame
    yield return null;                     // yield: the frame completes, Start() RUNS
    if (!_warmed)
        _pickup.gameObject.SetActive(false); // hide again — warm-up complete
    _warmed = true;
}
```

**Why this works:**
- `yield return null` suspends the coroutine until the next frame
- Before the coroutine resumes, Unity's end-of-frame processing executes all queued `Start()`
  calls — including `PickableItem.Start()` → `MMFeedbacks.Initialization()`
- When the coroutine resumes, `MMFeedbacks` is fully initialized and the pickup is hidden again
- The entire warm-up happens at **scene load time** when the enemy spawns — completely invisible
  to the player. The player is on a loading screen or has just entered the level; a 30–80ms hitch
  here is imperceptible
- From this point forward, every `SetActive(true)` on the pickup is instant because `Start()` has
  already run and will never run again

### The _warmed flag

A bool flag `_warmed` prevents a race condition:

```
Timeline (worst case):
  Frame 0: EnemyOrbitLoot.Start() — StampPickup(), start PreWarmPickup coroutine
            pickup.SetActive(true)    ← pre-warm begins
  Frame 1: (extremely rare) enemy is already dead at frame 1
            HandleDeath() fires → _warmed = true → SetActive(true) → pickup shown
            PreWarmPickup resumes → sees _warmed = true → does NOT hide pickup
```

Without the flag, the coroutine would call `SetActive(false)` on frame 1 after the pickup was
already made visible by death — hiding the loot drop that the player should see.

### Why embedding the pickup in the prefab matters

The pickup lives as an **inactive child in the enemy prefab** rather than being instantiated at
runtime:

| Approach                  | When is the freeze?    | Notes |
|---------------------------|------------------------|-------|
| Instantiate at death time | Death frame            | Freeze is visible |
| Instantiate at spawn time | Spawn frame            | Freeze on spawn, not death |
| Embedded child + same-frame toggle | Death frame | Toggle is a no-op, freeze persists |
| **Embedded child + coroutine pre-warm** | Scene load frame | **Invisible to player — used here** |

Using an embedded child means:
- No `Instantiate()` ever occurs during gameplay — zero GC allocation
- The pre-warm coroutine is the only `SetActive(true)` call that triggers `Start()`
- All subsequent activations (death, respawn cycles) are allocation-free and instant
- The pickup survives enemy disable/destroy because it is detached via `SetParent(null)` in `HandleDeath()`

### Summary of the guarantee

After `EnemyOrbitLoot.Start()` completes its two-frame coroutine:
- `PickableItem.Start()` has run ✓
- `MMFeedbacks.Initialization()` has run ✓
- All TDE feedback chains are built and cached ✓
- The pickup is inactive and ready ✓

Every call to `SetActive(true)` from that point on — at death or after respawn — does zero
initialization work. The frame budget impact is negligible.

---

## Runtime Flow (Complete)

```
SCENE LOADS
  Enemy.Awake()
    ├── EnemyOrbitLoot.Awake() → finds embedded OrbitWeaponPickup child (inactive)
    └── CharacterOrbitWeapons.Awake() → caches references

  Enemy.Start()
    ├── CharacterOrbitWeapons.Initialization()
    │     ├── GetOrCreatePivot() → finds/creates OrbitPivot child
    │     └── UpdateWeapons(WeaponCount)
    │           └── each sword: pool/instantiate → parent OrbitPivot
    │                 └── ApplyDefinition(WeaponDefinition) → sprite, stats
    └── EnemyOrbitLoot.Start()
          ├── StampPickup() → pickup.Init(WeaponCount, WeaponDefinition)
          └── StartCoroutine(PreWarmPickup())

  Frame boundary
    └── PreWarmPickup: SetActive(true) → PickableItem.Start() runs (MMFeedbacks initialized)
  Next frame
    └── PreWarmPickup resumes: SetActive(false) → pickup hidden, fully warm

PLAYER KILLS ENEMY
  Health.OnDeath
    └── EnemyOrbitLoot.HandleDeath()
          ├── _warmed = true
          ├── pickup.SetParent(null)         → detached from enemy
          ├── pickup.position = death + scatter
          └── pickup.SetActive(true)         → instant, no lag (Start already ran)

PLAYER WALKS OVER PICKUP
  CircleCollider2D trigger (via PickableItem)
    └── OrbitWeaponPickup.Pick(player)
          ├── orbit.WeaponDefinition = pickup.WeaponDefinition  ← sword type upgrade
          └── orbit.AddWeapons(PickupCount)
                └── new swords spawn with enemy's sword type applied

AFTER PICKUP
  DisableObjectOnPick = true → pickup deactivated by TDE

ENEMY RESPAWNS (if applicable)
  EnemyOrbitLoot.OnRespawn()
    ├── pickup.SetActive(false)
    ├── pickup.SetParent(enemy root)     → re-attach as child
    └── StampPickup()                    → re-stamp for next death cycle
```

---

## Quick Reference — Which Script Does What

| Script / Object          | Lives On              | Responsibility |
|--------------------------|-----------------------|----------------|
| `CharacterOrbitWeapons`  | Enemy root            | Spawns, pools, rotates, and manages orbiting swords |
| `OrbitPivot`             | Child of Enemy root   | Empty GameObject — rotates every frame to spin the sword ring |
| `EnemyOrbitLoot`         | Enemy root            | Pre-warms, stamps, and drops the weapon pickup on death |
| `OrbitWeaponCombat`      | OrbitSword prefab     | Per-sword combat: clashing, entity damage, health |
| `OrbitWeaponDefinition`  | ScriptableObject asset | Data: sprite, stats, cooldowns for one sword type |
| `OrbitWeaponPickup`      | Embedded child of Enemy | World pickup: displays sword info, grants swords on collection |
