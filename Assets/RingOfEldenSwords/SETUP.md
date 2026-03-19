# Ring of Elden Swords — Setup Guide

This document covers every prefab, script, and ScriptableObject in the project.
Use it as a reference when creating new enemies, weapon types, or scenes.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Prefabs](#prefabs)
   - [Player.prefab](#playerprefab)
   - [Enemy.prefab](#enemyprefab)
   - [OrbitSword.prefab](#orbitswordprefab)
   - [OrbitWeaponPickup.prefab](#orbitweaponpickupprefab)
3. [Scripts](#scripts)
   - [CharacterOrbitWeapons](#characterorbitweapons)
   - [OrbitWeaponCombat](#orbitweaponcombat)
   - [EnemyOrbitLoot](#enemyorbitloot)
   - [OrbitWeaponPickup](#orbitweaponpickup)
   - [OrbitWeaponDefinition](#orbitweapondefinition)
   - [GameStartEnemySpawner](#gamestarteneymyspawner)
4. [ScriptableObjects](#scriptableobjects)
5. [Creating a New Enemy Type](#creating-a-new-enemy-type)
6. [Creating a New Weapon Type](#creating-a-new-weapon-type)
7. [Prefab Variant Hierarchy](#prefab-variant-hierarchy)

---

## Architecture Overview

```
OrbitWeaponDefinition (ScriptableObject)
        │  defines sprite, stats, tint
        │
        ▼
CharacterOrbitWeapons (on Player / Enemy)
        │  spawns weapons, applies definition to each
        │
        ├──▶ OrbitWeaponCombat (on each OrbitSword instance)
        │         handles clash + entity damage
        │
        └──▶ EnemyOrbitLoot (on Enemy only)
                  on death → drops OrbitWeaponPickup
                                    │
                                    ▼
                             Player walks over it
                                    │
                                    ▼
                        OrbitWeaponPickup.Pick()
                                    │
                                    ▼
                        CharacterOrbitWeapons.AddWeapons()
```

---

## Prefabs

### Player.prefab

**Path:** `Assets/RingOfEldenSwords/Prefabs/Player/Player.prefab`

The player character. Built on TDE's `Character` component.

**Required components on root:**
| Component | Purpose |
|---|---|
| `Character` (TDE) | Core TDE character controller |
| `Health` (TDE) | Hit points, death/respawn events |
| `CharacterOrbitWeapons` | Manages the orbiting weapon ring |
| `CharacterMovement` (TDE) | 2D top-down movement |

**CharacterOrbitWeapons settings for Player:**
| Field | Recommended Value | Notes |
|---|---|---|
| Weapon Prefab | `OrbitSword.prefab` | The universal sword prefab |
| Weapon Definition | *(leave null)* | Player uses default prefab values; weapons picked up from enemies bring their own definition |
| Weapon Count | 12 | How many swords orbit at game start |
| Orbit Radius | 2 | Distance from player center to swords |
| Orbit Speed | 180 | Degrees/second |
| Arrival Duration | 0.5 | Sweep animation time in seconds |
| Spawn Angle Offset | -45 | Angle all swords start from before sweeping out |

> **Note:** Player currently has no WeaponDefinition assigned. When the player picks up enemy loot, `AddWeapons()` is called — new swords inherit whatever definition was last set, or the prefab defaults if none.

---

### Enemy.prefab

**Path:** `Assets/RingOfEldenSwords/Prefabs/Enemies/Enemy.prefab`

The base enemy prefab. All enemy variants should be **Prefab Variants** of this file.

**Required components on root:**
| Component | Purpose |
|---|---|
| `Character` (TDE) | Core TDE character controller |
| `Health` (TDE) | Hit points, `OnDeath` event used by loot system |
| `CharacterOrbitWeapons` | Manages the enemy's orbiting weapon ring |
| `EnemyOrbitLoot` | Spawns a hidden pickup at start, drops it on death |
| `CharacterMovement` (TDE) | Movement / AI |
| `AIBrain` (TDE) | Enemy AI state machine |

**CharacterOrbitWeapons settings for Enemy:**
| Field | Recommended Value | Notes |
|---|---|---|
| Weapon Prefab | `OrbitSword.prefab` | Same universal prefab as player |
| Weapon Definition | `WpnDef_BasicSword` | **Set this per enemy variant** |
| Weapon Count | 8 | Swords this enemy spawns with |
| Orbit Radius | 2 | |
| Orbit Speed | 180 | |

**EnemyOrbitLoot settings:**
| Field | Recommended Value | Notes |
|---|---|---|
| Pickup Prefab | `OrbitWeaponPickup.prefab` | The loot drop prefab |
| Scatter Radius | 1.5 | How far from the enemy center the pickup lands |

**How to create a new enemy variant (e.g. Fire Enemy):**
1. Right-click `Enemy.prefab` → **Create → Prefab Variant** → name it `Enemy_Fire`
2. Open `Enemy_Fire`, find `CharacterOrbitWeapons`
3. Set `Weapon Definition` → drag in `WpnDef_FireSword`
4. Optionally change `Weapon Count` for difficulty tuning

---

### OrbitSword.prefab

**Path:** `Assets/RingOfEldenSwords/Prefabs/Weapons/OrbitSword.prefab`
**Also at:** `Assets/RingOfEldenSwords/Resources/OrbitSword.prefab` (runtime fallback)

The **universal weapon prefab**. One prefab for all sword types — the `OrbitWeaponDefinition` stamps the correct sprite and stats at runtime.

**Required components:**
| Component | Purpose |
|---|---|
| `SpriteRenderer` | Displays the sword sprite. Tint and sprite set by `ApplyDefinition()` |
| `OrbitWeaponCombat` | Handles clash detection and entity damage |
| `Rigidbody2D` | Set to Kinematic at runtime by `CharacterOrbitWeapons` |
| `CircleCollider2D` (trigger) | Detects clash and entity hits |

**OrbitWeaponCombat Inspector fields** (defaults — overridden by WeaponDefinition at runtime):
| Field | Default | Notes |
|---|---|---|
| Max Health | 10 | HP before sword is destroyed in a clash |
| Clash Damage | 10 | Damage dealt to opposing sword per clash |
| Entity Damage | 10 | Damage dealt to a character on contact |
| Clash Cooldown | 0.1 | Seconds between clash ticks |
| Entity Hit Cooldown | 0.5 | Seconds between entity damage ticks |

> These values are the fallback when no `WeaponDefinition` is assigned. When a definition is assigned to `CharacterOrbitWeapons`, `ApplyDefinition()` overwrites all of them at spawn time.

---

### OrbitWeaponPickup.prefab

**Path:** `Assets/RingOfEldenSwords/Prefabs/ItemPickers/OrbitWeaponPickup.prefab`

The loot drop item that appears when an enemy dies. Extends TDE's `PickableItem`.

**Root GameObject** — `m_IsActive: 0` in YAML (spawns **inactive** by design to prevent physics trigger on instantiation).

**GameObject hierarchy:**
```
OrbitWeaponPickup (root)
├── Background        (SpriteRenderer — semi-transparent white circle)
└── CountText         (TextMeshPro — shows "x8" badge)
```

**Components on root:**
| Component | Purpose |
|---|---|
| `OrbitWeaponPickup` | Custom pickup logic |
| `SpriteRenderer` | Shows the weapon sprite (stamped at runtime by `EnemyOrbitLoot`) |
| `CircleCollider2D` (trigger) | TDE pickup detection |
| `SortingGroup` | Makes all children inherit the `Characters` sorting layer |

**OrbitWeaponPickup Inspector fields:**
| Field | Notes |
|---|---|
| Weapon Sprite | Auto-wired to root `SpriteRenderer` on Start. Can assign manually. |
| Count Text | Auto-wired to `CountText` child TMP on Start. Can assign manually. |

**TDE PickableItem inherited fields (set on this prefab):**
| Field | Value | Notes |
|---|---|---|
| Require Player Type | true | Only player characters can pick this up |
| Disable Object On Pick | true | Hides pickup after collection |

> **Do not place this prefab in the scene manually.** It is always instantiated at runtime by `EnemyOrbitLoot` as a hidden child of the enemy, then activated on death.

---

## Scripts

### CharacterOrbitWeapons

**Path:** `Assets/RingOfEldenSwords/Scripts/Characters/CharacterAbilities/CharacterOrbitWeapons.cs`
**Namespace:** `RingOfEldenSwords.Character.Abilities`
**Inherits:** `CharacterAbility` (TDE)

Manages the ring of orbiting weapons around any character (player or enemy). Attach to the **root** of any character that should have orbiting weapons.

#### Inspector Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `WeaponPrefab` | GameObject | — | The universal OrbitSword prefab. Assign once, never change. |
| `WeaponDefinition` | OrbitWeaponDefinition | null | ScriptableObject defining this character's weapon type. Leave null to use prefab defaults. |
| `WeaponCount` | int | 3 | Number of weapons to spawn at game start and after respawn. |
| `OrbitRadius` | float | 2 | Distance in world units from character center to each weapon. |
| `OrbitSpeed` | float | 180 | Degrees per second the orbit ring rotates. |
| `ArrivalDuration` | float | 0.5 | Seconds each weapon takes to sweep into orbit position on spawn. |
| `SpawnAngleOffset` | float | -45 | Angle (degrees) all weapons spawn from before sweeping outward. |
| `SweepCurve` | AnimationCurve | Linear | Easing curve for the sweep animation. |

#### Public API

| Method / Property | Description |
|---|---|
| `AddWeapons(int count)` | Adds `count` weapons to the current ring. Called by `OrbitWeaponPickup` on pickup. |
| `UpdateWeapons(int count)` | Replaces the entire ring with `count` new weapons. |
| `StartOrbit()` / `StopOrbit()` | Manually start/stop pivot rotation. |
| `WeaponDefinition` | The assigned SO. Read by `EnemyOrbitLoot` to get the sprite for the loot icon. |
| `Weapons` | Read-only list of all active weapon GameObjects. |
| `ActiveWeaponCount` | How many weapons are currently orbiting. |
| `WeaponCount` | Current weapon count (updates when weapons are added/removed). |

#### TDE Lifecycle Hooks Used

| Hook | What it does here |
|---|---|
| `Initialization()` | Creates OrbitPivot child, spawns starting weapons |
| `ProcessAbility()` | Rotates the pivot every frame |
| `OnDeath()` | Stops rotation, removes immunity |
| `OnRespawn()` | Restores starting weapon count |
| `ResetAbility()` | Returns all weapons to pool before respawn |

---

### OrbitWeaponCombat

**Path:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponCombat.cs`
**Namespace:** `RingOfEldenSwords.Combat.Weapons`
**Inherits:** `MonoBehaviour`

Lives on each `OrbitSword` instance. Handles clash detection against opposing faction weapons and contact damage against characters.

#### Inspector Parameters (defaults — overridden by WeaponDefinition)

| Parameter | Type | Default | Description |
|---|---|---|---|
| `MaxHealth` | float | 10 | Weapon HP. Drops to 0 via clashes → weapon destroyed. |
| `ClashDamage` | float | 10 | Damage dealt to an opposing weapon per clash. |
| `EntityDamage` | int | 10 | Damage dealt to a character's `Health` component on contact. |
| `ClashCooldown` | float | 0.1 | Minimum seconds between clash ticks. |
| `EntityHitCooldown` | float | 0.5 | Minimum seconds between entity damage ticks. |

#### Public API

| Method / Property | Description |
|---|---|
| `ApplyDefinition(OrbitWeaponDefinition def)` | Stamps all stats and visuals from a SO. Called by `CharacterOrbitWeapons` at spawn. |
| `ResetHealth()` | Resets HP to `MaxHealth`. Called when reusing a pooled weapon. |
| `TakeDamage(float amount)` | Reduces HP. If reaches 0, fires `OnDestroyed` and destroys the GameObject. |
| `GetSprite()` | Returns current sprite from the SpriteRenderer. |
| `IsAlive` | True if HP > 0 and state is Active. |
| `CurrentHealth` | Current HP value. |
| `OnDestroyed` | Event fired with the weapon GameObject just before `Destroy()`. |
| `OnClash` | Event fired with the opposing `OrbitWeaponCombat` when a clash occurs. |

#### Faction Logic

Faction is determined by **GameObject tag** set at spawn time by `CharacterOrbitWeapons`:
- Player-owned weapons get tag `"Player"` → only damage `"Enemy"` tagged characters
- Enemy-owned weapons get tag `"Enemy"` → only damage `"Player"` tagged characters

---

### EnemyOrbitLoot

**Path:** `Assets/RingOfEldenSwords/Scripts/Enemy/EnemyOrbitLoot.cs`
**Namespace:** `RingOfEldenSwords.Enemy`
**Inherits:** `TopDownMonoBehaviour` (TDE)

Attach alongside `CharacterOrbitWeapons` on any enemy root. Manages the full loot lifecycle:
- **At spawn:** Instantiates a hidden `OrbitWeaponPickup` child and stamps it with weapon count + sprite
- **At death:** Detaches the pickup, scatters it, activates it
- **At respawn:** Creates a fresh hidden pickup for the next death

#### Inspector Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `_pickupPrefab` | GameObject | — | Must be `OrbitWeaponPickup.prefab`. Assign in Inspector. |
| `_scatterRadius` | float | 1.5 | How far (world units) from the death position the pickup lands. |

#### Sprite Resolution Priority

When stamping the pickup icon, `EnemyOrbitLoot` looks for the sprite in this order:

1. `CharacterOrbitWeapons.WeaponDefinition.Sprite` — cheapest, most reliable
2. First active orbiting weapon's `SpriteRenderer` — live instance fallback
3. `WeaponPrefab.SpriteRenderer` — prefab asset fallback

Always assign a `WeaponDefinition` to ensure the correct icon appears.

---

### OrbitWeaponPickup

**Path:** `Assets/RingOfEldenSwords/Scripts/Combat/Pickups/OrbitWeaponPickup.cs`
**Namespace:** `RingOfEldenSwords.Combat.Pickups`
**Inherits:** `PickableItem` (TDE)

The pickup item dropped by enemies. TDE's `PickableItem` base class handles all collision detection, player-type gating, and disable-on-pick logic.

#### Inspector Parameters

| Parameter | Type | Description |
|---|---|---|
| `_weaponSprite` | SpriteRenderer | Root SpriteRenderer showing the weapon icon. Auto-wired on Start if not assigned. |
| `_countText` | TextMeshPro | TMP showing the count badge (e.g. "x8"). Auto-wired on Start if not assigned. |

#### Inherited TDE PickableItem Parameters (set on prefab)

| Parameter | Value | Description |
|---|---|---|
| `RequirePlayerType` | true | Only `CharacterTypes.Player` characters can pick this up. |
| `DisableObjectOnPick` | true | Hides the pickup GameObject after collection. |

#### Public API

| Method / Property | Description |
|---|---|
| `Init(int count, Sprite sprite)` | Called by `EnemyOrbitLoot` to stamp weapon count and sprite onto this pickup. |
| `PickupCount` | How many weapons this pickup grants. Set by `Init()`. |

#### Pick Flow

```
Player walks into trigger
    → PickableItem.OnTriggerEnter2D()     (TDE base)
    → PickableItem.PickItem()             (TDE base — checks RequirePlayerType)
    → OrbitWeaponPickup.Pick()            (our override)
    → CharacterOrbitWeapons.AddWeapons(PickupCount)
```

---

### OrbitWeaponDefinition

**Path:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponDefinition.cs`
**Namespace:** `RingOfEldenSwords.Combat.Weapons`
**Inherits:** `ScriptableObject`

Data asset for a single weapon type. Create one asset per weapon type — no code or prefabs needed.

**Create:** Right-click in Project → **Create → RingOfEldenSwords → Orbit Weapon Definition**

#### All Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `WeaponName` | string | "Basic Sword" | Display name for editor and debug logs. |
| `Sprite` | Sprite | — | Sprite applied to the weapon's SpriteRenderer at spawn. Also used as the loot pickup icon. |
| `Tint` | Color | White | Color tint applied to the SpriteRenderer. White = no tint. Red tint = fire sword appearance, etc. |
| `MaxHealth` | float | 10 | Weapon HP before a clash destroys it. |
| `ClashDamage` | float | 10 | Damage dealt to an opposing weapon per clash. Higher = destroys enemy weapons faster. |
| `EntityDamage` | int | 10 | Damage per hit dealt to a character's `Health` component. |
| `ClashCooldown` | float | 0.1 | Min seconds between clash damage ticks. Lower = faster clash resolution. |
| `EntityHitCooldown` | float | 0.5 | Min seconds between entity damage ticks. Lower = faster attack rate. |

#### Recommended Naming Convention

```
WpnDef_BasicSword.asset
WpnDef_FireSword.asset
WpnDef_IceSword.asset
WpnDef_PoisonSword.asset
WpnDef_ThunderSword.asset
```

---

### GameStartEnemySpawner

**Path:** `Assets/RingOfEldenSwords/Scripts/GameStartEnemySpawner.cs`
**Namespace:** `RingOfEldenSwords`
**Inherits:** `MonoBehaviour`

Place on any GameObject in the scene. Spawns a fixed number of enemies evenly across a rectangular area on scene start. No waves, no timers.

#### Inspector Parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `enemyPrefab` | GameObject | — | The enemy prefab to spawn (e.g. `Enemy.prefab` or a variant). |
| `enemyCount` | int | 5 | Total enemies to spawn. |
| `areaCenter` | Vector2 | (0,0) | World-space centre of the spawn rectangle. |
| `areaExtents` | Vector2 | (8,6) | Half-width and half-height of the spawn rectangle. |
| `minDistanceFromCenter` | float | 3 | Enemies will not spawn closer than this distance from (0,0) — keeps the player start area clear. |

> A red wire box and sphere are drawn in the Scene view when this GameObject is selected, showing the spawn area and exclusion zone.

---

## ScriptableObjects

All weapon definition assets live here (create the folder if it doesn't exist):

```
Assets/RingOfEldenSwords/ScriptableObjects/Weapons/
├── WpnDef_BasicSword.asset
├── WpnDef_FireSword.asset
└── WpnDef_IceSword.asset    ← etc.
```

### Example: WpnDef_FireSword

| Field | Value |
|---|---|
| Weapon Name | Fire Sword |
| Sprite | fire_sword sprite |
| Tint | (255, 120, 30, 255) — orange |
| Max Health | 8 |
| Clash Damage | 15 |
| Entity Damage | 20 |
| Clash Cooldown | 0.1 |
| Entity Hit Cooldown | 0.4 |

### Example: WpnDef_IceSword

| Field | Value |
|---|---|
| Weapon Name | Ice Sword |
| Sprite | ice_sword sprite |
| Tint | (180, 220, 255, 255) — light blue |
| Max Health | 15 |
| Clash Damage | 8 |
| Entity Damage | 12 |
| Clash Cooldown | 0.15 |
| Entity Hit Cooldown | 0.6 |

---

## Creating a New Enemy Type

**Full walkthrough — no code required.**

### 1. Create the weapon definition

```
Right-click in Project
→ Create → RingOfEldenSwords → Orbit Weapon Definition
→ Name it WpnDef_ThunderSword
```

Fill in sprite, tint (yellow), and stats.

### 2. Create the enemy prefab variant

```
Right-click Enemy.prefab
→ Create → Prefab Variant
→ Name it Enemy_Thunder
```

### 3. Configure the variant

Open `Enemy_Thunder.prefab`. On `CharacterOrbitWeapons`:

```
Weapon Definition  →  WpnDef_ThunderSword   (drag asset here)
Weapon Count       →  6                     (or any value)
```

Everything else (movement, health, loot system, combat) is inherited from `Enemy.prefab`.

### 4. Drop it in the scene

Done. When `Enemy_Thunder` dies, it drops a pickup showing the thunder sword sprite and count.

---

## Creating a New Weapon Type

Only one step: **create a ScriptableObject asset**. No prefabs, no code.

```
Right-click in ScriptableObjects/Weapons/
→ Create → RingOfEldenSwords → Orbit Weapon Definition
→ Fill in fields
→ Assign to an enemy prefab variant's CharacterOrbitWeapons.WeaponDefinition
```

---

## Prefab Variant Hierarchy

```
Enemy.prefab                          ← base: all shared logic
├── Enemy_BasicSword.prefab           ← variant: WpnDef_BasicSword, count 8
├── Enemy_FireSword.prefab            ← variant: WpnDef_FireSword,  count 6
├── Enemy_IceSword.prefab             ← variant: WpnDef_IceSword,   count 10
└── Enemy_ThunderSword.prefab         ← variant: WpnDef_Thunder,    count 5

(Future: character types as intermediate variants)

Enemy.prefab
├── Enemy_Ninja.prefab                ← variant: Ninja sprite + animator
│   ├── Enemy_Ninja_Fire.prefab       ← variant of variant: WpnDef_FireSword only
│   └── Enemy_Ninja_Ice.prefab        ← variant of variant: WpnDef_IceSword only
└── Enemy_Samurai.prefab              ← variant: Samurai sprite + animator
    ├── Enemy_Samurai_Fire.prefab
    └── Enemy_Samurai_Ice.prefab
```

Fix a bug in `EnemyOrbitLoot`? Change it in `Enemy.prefab` — all variants get it instantly.
Add a new sword type? Create one `.asset` file, drag it onto a variant. No code touched.
