# Enemy Prefab — Setup Guide

**Prefab path:** `Assets/RingOfEldenSwords/Prefabs/Enemies/Enemy.prefab`

---

## Overview

The Enemy prefab is the base for all enemies in Ring of Elden Swords.
It uses Unity **Prefab Variants** — create a variant per enemy type, override only what differs (WeaponDefinition, WeaponCount, Health). All shared logic lives here and propagates to every variant automatically.

```
Enemy.prefab                          ← you are here (base)
├── Enemy_Ninja.prefab   (variant)    ← override: sprite, animator, WeaponDefinition
│   ├── Enemy_Ninja_L1.prefab         ← override: WeaponDefinition = Sword_Level_1
│   └── Enemy_Ninja_L5.prefab         ← override: WeaponDefinition = Sword_Level_5
└── Enemy_Samurai.prefab (variant)    ← override: sprite, animator, WeaponDefinition
```

---

## Components on the Root GameObject

### 1. CharacterOrbitWeapons
**Script:** `Assets/RingOfEldenSwords/Scripts/Characters/CharacterAbilities/CharacterOrbitWeapons.cs`
**Base class:** TDE `CharacterAbility`

Manages the ring of orbiting weapons around this enemy.

| Inspector Field | Type | Description |
|---|---|---|
| **Weapon Prefab** | GameObject | The universal orbit sword prefab. Set to `OrbitSword.prefab`. Never change this per variant — it is always the same shell prefab. |
| **Weapon Definition** | OrbitWeaponDefinition | The ScriptableObject that defines this enemy's sword type (sprite, stats). **This is what you change per variant.** |
| **Weapon Count** | int | How many swords orbit this enemy at spawn. |
| **Orbit Radius** | float | Distance from enemy center to each sword (world units). |
| **Orbit Speed** | float | Degrees per second the ring rotates. |
| **Arrival Duration** | float | Seconds each sword takes to sweep into orbit position on spawn. |
| **Spawn Angle Offset** | float | Angle (degrees) all swords start from before sweeping outward. |
| **Sweep Curve** | AnimationCurve | Easing for the spawn sweep animation. |

**How it works at runtime:**
1. `Initialization()` runs → calls `UpdateWeapons(WeaponCount)`
2. Each sword is spawned/pooled, tagged `"Enemy"`, and `ApplyDefinition(WeaponDefinition)` is called on its `OrbitWeaponCombat`
3. Swords sweep into orbit position using `SweepCurve`
4. Once all swords arrive → `ImmuneToDamage = true` (sword shield active)
5. On death → `ImmuneToDamage = false`, swords removed

---

### OrbitPivot — Required Child GameObject

`CharacterOrbitWeapons` requires a child GameObject named exactly **`OrbitPivot`** on the enemy (and player) root.

```
Enemy (root)
└── OrbitPivot          ← this GameObject
    ├── OrbitSword(Clone)
    ├── OrbitSword(Clone)
    └── OrbitSword(Clone)
```

**What it does:** The pivot's Z rotation is incremented every frame by `OrbitSpeed`. Swords are parented to it, so they all rotate together as a ring without the enemy sprite rotating. The enemy root and its sprite are never touched.

**Setup:** `CharacterOrbitWeapons.Initialization()` calls `GetOrCreatePivot()` which:
- Searches for an existing child named `"OrbitPivot"`
- If not found, creates one automatically at `localPosition = Vector3.zero`

> **You do not need to create `OrbitPivot` manually.** It is created at runtime on first play. However if you want to see it in the Editor hierarchy during edit mode, you can add an empty child GameObject named `OrbitPivot` to the prefab — the script will find and reuse it.

**Important:** Do not rename, delete, or reparent `OrbitPivot`. Do not add any visual components to it. It is a pure rotation driver — completely invisible.

---

### 2. EnemyOrbitLoot
**Script:** `Assets/RingOfEldenSwords/Scripts/Enemy/EnemyOrbitLoot.cs`
**Base class:** TDE `TopDownMonoBehaviour`

Handles the weapon pickup dropped when this enemy dies.

| Inspector Field | Type | Description |
|---|---|---|
| **Pickup Prefab** | GameObject | Set to `OrbitWeaponPickup.prefab`. This is the loot drop item. |
| **Scatter Radius** | float | How far (world units) the pickup scatters from the death position. Default: `1.5`. |

**How it works at runtime:**
1. `Start()` → instantiates `OrbitWeaponPickup` as an **inactive** child (hidden, no physics)
2. On death (`Health.OnDeath`) → `StampPickup()` reads `WeaponDefinition` + `WeaponCount` from `CharacterOrbitWeapons` and calls `pickup.Init(count, definition)`
3. Pickup is detached from enemy, scattered, then `SetActive(true)`
4. On respawn → fresh pickup is created for the next death

> **Why stamp at death, not at spawn?**
> `CharacterOrbitWeapons.Initialization()` runs in `Start()` — same frame as `EnemyOrbitLoot.Start()`. Stamping at death guarantees `Initialization()` has already run and `WeaponDefinition` is valid.

---

### 3. OrbitWeaponCombat
**Script:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponCombat.cs`
**Base class:** `MonoBehaviour`
**Lives on:** `OrbitSword.prefab` (the weapon prefab, not the enemy root)

Handles combat for each individual orbiting sword instance.

| Field | Type | Description |
|---|---|---|
| **Max Health** | float | HP of this sword. Reaches 0 via clashes → sword destroyed. Overridden by `WeaponDefinition`. |
| **Clash Damage** | float | Damage dealt to opposing sword on collision. Overridden by `WeaponDefinition`. |
| **Entity Damage** | int | Damage dealt to a character's `Health` on contact. Overridden by `WeaponDefinition`. |
| **Clash Cooldown** | float | Min seconds between clash damage ticks. |
| **Entity Hit Cooldown** | float | Min seconds between entity damage ticks. |

**Faction logic:** sword tag is set to `"Enemy"` by `CharacterOrbitWeapons` at spawn. Enemy swords only damage `Player`-tagged characters and vice versa — matching TDE's tag convention.

**Key events:**
- `OnDestroyed` — fired when sword HP reaches 0. `CharacterOrbitWeapons` listens to remove it from the active list.
- `OnClash` — fired when this sword clashes with an opposing sword.

---

## Weapon Definition (ScriptableObject)

**Class:** `OrbitWeaponDefinition`
**Script:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponDefinition.cs`
**Assets:** `Assets/RingOfEldenSwords/ScriptableObjects/Weapons/Swords/`

This is the **single source of truth** for a sword type. Assign one per enemy variant. Creating a new sword type requires no code and no new prefab — just a new asset.

### How to create a new Weapon Definition
```
Right-click in Project window
→ Create → RingOfEldenSwords → Orbit Weapon Definition
→ Name it (e.g. Sword_Level_5)
→ Fill in the fields below
```

| Field | Type | Description |
|---|---|---|
| **Weapon Name** | string | Display name used in editor and debug logs. |
| **Sprite** | Sprite | The sprite shown on orbiting swords and on the loot drop pickup. |
| **Max Health** | float | Sword HP before it is destroyed by clashes. |
| **Clash Damage** | float | Damage dealt to opposing swords on collision. |
| **Entity Damage** | int | Damage dealt to a character on contact. |
| **Clash Cooldown** | float | Minimum seconds between clash ticks (default: `0.1`). |
| **Entity Hit Cooldown** | float | Minimum seconds between entity damage ticks (default: `0.5`). |

### Pre-made Sword Definitions (48 levels)

All 48 levels are in `Assets/RingOfEldenSwords/ScriptableObjects/Weapons/Swords/`.

| Level Range | Stats (Health / Clash / Entity) |
|---|---|
| Level 1 | 10 / 10 / 10 |
| Level 2 | 15 / 15 / 15 |
| Level 5 | 30 / 30 / 30 |
| Level 10 | 55 / 55 / 55 |
| Level 20 | 105 / 105 / 105 |
| Level 48 | 245 / 245 / 245 |

**Formula:** `Value = 10 + (Level - 1) × 5`

---

## Loot Drop Pickup

**Prefab:** `Assets/RingOfEldenSwords/Prefabs/ItemPickers/OrbitWeaponPickup.prefab`
**Script:** `Assets/RingOfEldenSwords/Scripts/Combat/Pickups/OrbitWeaponPickup.cs`
**Base class:** TDE `PickableItem`

The world item that appears when an enemy dies. Shows the sword sprite and count badge.

| Inspector Field | Type | Description |
|---|---|---|
| **Weapon Sprite** | SpriteRenderer | The SpriteRenderer that displays the sword icon. Auto-wired from root if not set. |
| **Count Text** | TextMeshPro | TMP component showing `x3` style badge. Auto-wired from children if not set. |

**Inherited from TDE PickableItem:**

| Field | Description |
|---|---|
| **Require Player Type** | Set to `Player` so only the player can pick this up. |
| **Disable Object On Pick** | Set to `true` so pickup disappears after collection. |

**What happens on pickup:**
1. Player walks over pickup → `PickableItem.OnTriggerEnter2D` fires
2. TDE validates: correct character type, item is pickable
3. `Pick(picker)` is called → finds player's `CharacterOrbitWeapons`
4. Sets `orbit.WeaponDefinition = pickup.WeaponDefinition` — **player now has the enemy's sword type**
5. Calls `orbit.AddWeapons(PickupCount)` — new swords spawn with the correct sprite and stats

---

## Creating a New Enemy Variant (Step by Step)

### Example: Enemy with Level 10 Swords

1. **Right-click** `Enemy.prefab` in Project → **Create → Prefab Variant**
2. Name it `Enemy_Sword_L10`
3. Open `Enemy_Sword_L10` in Prefab Mode
4. Select the root GameObject
5. In **Character Orbit Weapons**:
   - Set `Weapon Definition` → drag `Sword_Level_10.asset`
   - Set `Weapon Count` → e.g. `6`
6. Save — done

The enemy now spawns with 6 Level-10 swords. When it dies, it drops a pickup showing the Level-10 sword sprite and count. When the player picks it up, their swords upgrade to Level-10 type.

---

## Runtime Flow (Complete)

```
Scene loads
  └── Enemy.Start()
        ├── CharacterOrbitWeapons.Initialization()
        │     ├── GetOrCreatePivot() → finds or creates "OrbitPivot" child
        │     └── UpdateWeapons(WeaponCount)
        │           └── each sword: Instantiate/pool → parent to OrbitPivot
        │                 └── ApplyDefinition(WeaponDefinition)
        │                       → sets sprite, MaxHealth, ClashDamage, EntityDamage
        └── EnemyOrbitLoot.Start()
              └── SpawnHiddenPickup() → inactive child, not yet stamped

Player kills enemy
  └── Health.OnDeath fires
        └── EnemyOrbitLoot.HandleDeath()
              ├── StampPickup()
              │     └── reads orbit.WeaponDefinition + orbit.WeaponCount
              │           └── pickup.Init(count, definition) → sprite + count set
              ├── pickup.SetParent(null) → detached from enemy
              ├── pickup.position = deathPos + scatter
              └── pickup.SetActive(true) → visible, physics active

Player walks over pickup
  └── PickableItem.OnTriggerEnter2D
        └── OrbitWeaponPickup.Pick(player)
              ├── player.orbit.WeaponDefinition = pickup.WeaponDefinition
              └── player.orbit.AddWeapons(PickupCount)
                    └── new swords spawn with enemy's sword type
```

---

## Quick Reference — Which Script Does What

| Script / Object | Lives On | Responsibility |
|---|---|---|
| `CharacterOrbitWeapons` | Enemy root | Spawns, pools, rotates, and manages orbiting swords |
| `OrbitPivot` | Child of Enemy root | Empty GameObject — rotates every frame to spin the sword ring |
| `EnemyOrbitLoot` | Enemy root | Creates and drops the weapon pickup on death |
| `OrbitWeaponCombat` | OrbitSword prefab | Per-sword combat: clashing, entity damage, health |
| `OrbitWeaponDefinition` | ScriptableObject asset | Data: sprite, stats, cooldowns for one sword type |
| `OrbitWeaponPickup` | OrbitWeaponPickup prefab | World pickup: displays sword info, grants swords on collection |
