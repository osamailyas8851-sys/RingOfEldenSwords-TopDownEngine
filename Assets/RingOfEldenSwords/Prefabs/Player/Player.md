# Player Prefab — Setup Guide

**Prefab path:** `Assets/RingOfEldenSwords/Prefabs/Player/Player.prefab`

---

## Prefab Structure

```
Player                           (root — Tag: "Player", Layer: 10)
├── Rigidbody2D                  ← physics body, driven by TopDownController2D
├── BoxCollider2D                ← player body collider
│
├── Character                    ← TDE core: ties all abilities together
├── TopDownController2D          ← TDE: reads input, moves Rigidbody2D
├── CharacterOrientation2D       ← TDE: flips sprite to face movement direction
├── CharacterMovement            ← TDE: walk speed, acceleration
├── CharacterRun                 ← TDE: run speed multiplier
├── CharacterDash2D              ← TDE: dash ability
├── CharacterJump2D              ← TDE: jump ability (if used in top-down)
├── CharacterButtonActivation    ← TDE: interact/activate button
├── CharacterPause               ← TDE: pause menu hook
├── CharacterTimeControl         ← TDE: slow-motion / time abilities
├── CharacterHandleWeapon        ← TDE: standard weapon slot (unused by orbit system)
├── CharacterInventory           ← TDE: inventory system
├── Health                       ← TDE: player HP, death, respawn
├── CharacterOrbitWeapons        ← CUSTOM: manages the orbiting sword ring
│
├── MinimalCharacterModel        (child — sprite lives here)
│   └── SpriteRenderer           ← player sprite
│
├── WeaponAttachment             (child — TDE standard weapon mount point)
│
└── OrbitPivot                   (child — sword ring rotation driver)
    ├── OrbitSword(Clone)        ← spawned at runtime
    ├── OrbitSword(Clone)
    └── ...
```

---

## Components on Root — TDE (Built-in)

These are standard TopDown Engine components. They are configured in the Inspector but not custom code. Brief descriptions for reference:

| Component | Purpose |
|---|---|
| **Character** | TDE core brain. Ties all `CharacterAbility` components together. Sets `CharacterType = Player`. |
| **TopDownController2D** | Reads input from `InputManager` and moves the `Rigidbody2D`. Handles slopes, collision detection. |
| **CharacterOrientation2D** | Flips or rotates the `MinimalCharacterModel` sprite to face the movement or aim direction. |
| **CharacterMovement** | Walk speed, acceleration, deceleration. |
| **CharacterRun** | Run speed multiplier, triggered by hold-run input. |
| **CharacterDash2D** | Dash ability — speed, duration, cooldown, direction. |
| **CharacterJump2D** | Jump ability (can be used for dodge in top-down games). |
| **CharacterButtonActivation** | Activates interactable objects (doors, levers) on button press. |
| **CharacterPause** | Hooks the pause button to TDE's pause system. |
| **CharacterTimeControl** | Slow-motion / bullet-time ability. |
| **CharacterHandleWeapon** | Standard TDE weapon slot. Not used by the orbit system — orbit weapons are managed by `CharacterOrbitWeapons` separately. |
| **CharacterInventory** | TDE inventory system. |
| **Health** | Player HP. Fires `OnDeath`, `OnHit`, `OnRevive` events. `CharacterOrbitWeapons` listens to `OnDeath` to clear swords. |

**Key Health values (set in prefab):**

| Field | Value |
|---|---|
| `CurrentHealth` | `100` |
| `MaximumHealth` | `100` |
| `ImmuneToDamage` | `false` (set to `true` at runtime by `CharacterOrbitWeapons` when swords are orbiting) |

---

## Custom Component — CharacterOrbitWeapons

**Script:** `Assets/RingOfEldenSwords/Scripts/Characters/CharacterAbilities/CharacterOrbitWeapons.cs`
**Base class:** TDE `CharacterAbility`

The only custom script on the Player root. Manages the full orbit weapon ring.

### Inspector Fields

| Field | Type | Value in Prefab | Description |
|---|---|---|---|
| **Weapon Prefab** | GameObject | `OrbitSword.prefab` | The sword prefab to spawn. One universal prefab for all sword types. Never change this. |
| **Weapon Definition** | OrbitWeaponDefinition | `null` (starts empty) | The sword type the player currently has. `null` at game start — player begins with no sword type until they pick one up from an enemy drop. Updated at runtime by `OrbitWeaponPickup.Pick()`. |
| **Weapon Count** | int | `12` | How many swords orbit the player at game start and after respawn. |
| **Orbit Radius** | float | `2` | Distance from player center to each sword (world units). |
| **Orbit Speed** | float | `180` | Degrees per second the ring rotates. `180` = half rotation per second. |
| **Arrival Duration** | float | `0.5` | Seconds each sword takes to sweep into orbit position on spawn. |
| **Spawn Angle Offset** | float | `-45` | Angle all swords start from before sweeping outward. |
| **Sweep Curve** | AnimationCurve | Linear | Easing curve for the spawn sweep animation. |

### How It Works at Runtime

1. `Initialization()` (called by TDE at scene start) → `GetOrCreatePivot()` → `UpdateWeapons(12)`
2. 12 `OrbitSword` instances are spawned/pooled and parented to `OrbitPivot`
3. Each sword is tagged `"Player"` and `ApplyDefinition(WeaponDefinition)` is called — if `WeaponDefinition` is null, swords use the prefab's default stats
4. Swords sweep into evenly-spaced positions over `0.5s`
5. Once all 12 arrive → `ImmuneToDamage = true` on the player's `Health` component (sword shield)
6. `ProcessAbility()` runs every frame → rotates `OrbitPivot` by `180° × deltaTime`

### Key Lifecycle Hooks (TDE CharacterAbility)

| Override | When called | What it does |
|---|---|---|
| `Initialization()` | Scene start | Creates pivot, spawns starting swords |
| `ProcessAbility()` | Every frame | Rotates OrbitPivot |
| `OnDeath()` | Player dies | `_isRotating = false`, `ImmuneToDamage = false` |
| `OnRespawn()` | Player respawns | `UpdateWeapons(WeaponCount)` — restores starting sword count |
| `ResetAbility()` | Before respawn | Returns all swords to pool |

### Public API (callable from other scripts)

| Method | Description |
|---|---|
| `AddWeapons(int count)` | Adds `count` swords to current ring. Called by `OrbitWeaponPickup.Pick()` when player collects a drop. |
| `UpdateWeapons(int count)` | Rebuilds entire ring with new count. Resets and respawns all swords. |
| `StartOrbit()` / `StopOrbit()` | Starts/stops pivot rotation manually. |

---

## Child GameObjects

### MinimalCharacterModel

The player's visible sprite. Separated from the root so `CharacterOrientation2D` can flip it independently without affecting the orbit pivot or colliders.

| Component | Description |
|---|---|
| **SpriteRenderer** | Player sprite. Flipped by `CharacterOrientation2D` to face movement direction. |

### WeaponAttachment

Standard TDE child for mounting held weapons (e.g. guns, melee). Not used by the orbit system — left for TDE compatibility so `CharacterHandleWeapon` can find it.

### OrbitPivot

The invisible rotation driver for the sword ring. **Already present in the prefab** (unlike the Enemy prefab where it is created at runtime).

| Property | Value |
|---|---|
| Layer | `0` (Default) |
| Tag | `Untagged` |
| Components | Transform only — no visuals, no colliders |

`CharacterOrbitWeapons.GetOrCreatePivot()` finds this by name `"OrbitPivot"` and reuses it. All spawned `OrbitSword` instances are parented here at runtime.

> **Do not rename, delete, or add components to `OrbitPivot`.** It is a pure rotation driver.

---

## Workflow — How the Player Interacts With Other Systems

```
1. SCENE LOADS
   Character.Start() → TDE initialises all CharacterAbility components
     └── CharacterOrbitWeapons.Initialization()
           ├── GetOrCreatePivot() → finds existing "OrbitPivot" child
           └── UpdateWeapons(12)
                 └── 12 × OrbitSword spawned → tagged "Player"
                       └── WeaponDefinition = null → prefab default stats used
                             → all 12 swords sweep into orbit ring
                             → ImmuneToDamage = true (sword shield ON)

2. PLAYER WALKS OVER LOOT DROP
   OrbitWeaponPickup.OnTriggerEnter2D (via TDE PickableItem)
     └── Pick(player)
           ├── orbit.WeaponDefinition = Sword_Level_5
           │     → player now has a weapon type
           └── orbit.AddWeapons(6)
                 └── 6 new OrbitSwords spawned with Sword_Level_5 stats
                       → MaxHealth=30, ClashDamage=30, EntityDamage=30
                       → Sprite = Sword_Level_5 sprite

3. PLAYER SWORDS HIT ENEMY
   OrbitSword.BladeHitbox.OnTriggerEnter2D
     └── OrbitWeaponCombat.OnTriggerEnter2D
           ├── tag="Player", target tag="Enemy" → allowed
           └── Health.Damage(EntityDamage, ...)

4. PLAYER SWORDS CLASH WITH ENEMY SWORDS
   OrbitWeaponCombat.PerformClash()
     ├── player sword takes damage from enemy clash damage
     ├── enemy sword takes damage from player clash damage
     └── if player sword HP = 0 → DestroyWeapon()
           └── OnDestroyed → CharacterOrbitWeapons.HandleWeaponDestroyed()
                 ├── removes from _activeWeapons
                 └── if last sword → ImmuneToDamage = false (shield OFF)

5. PLAYER DIES
   Health.OnDeath
     └── CharacterOrbitWeapons.OnDeath()
           └── _isRotating = false, ImmuneToDamage = false

6. PLAYER RESPAWNS
   Health.OnRevive → CharacterOrbitWeapons.OnRespawn()
     └── UpdateWeapons(12) → restores full starting sword ring
```

---

## Quick Reference — Player-Specific Rules

| Rule | Reason |
|---|---|
| **`WeaponDefinition` starts null** | Player has no sword type at game start. The first enemy loot drop they collect sets their definition. Until then, swords use `OrbitSword.prefab` default stats. |
| **`WeaponCount: 12` is the respawn count** | This is what `OnRespawn()` restores. It does not cap the total — `AddWeapons()` can exceed it during a run. |
| **`ImmuneToDamage` is managed by `CharacterOrbitWeapons`** | Do not set it manually in the prefab or other scripts. It turns on when all swords are orbiting, off when the last sword is destroyed or the player dies. |
| **`CharacterHandleWeapon` is present but unused** | TDE requires it for the standard weapon system. Leave it — removing it may break TDE internals. |
| **`OrbitPivot` is pre-created in the prefab** | Unlike the Enemy prefab where it is auto-created at runtime, the Player prefab already has `OrbitPivot` as a child. `GetOrCreatePivot()` finds and reuses it. |
