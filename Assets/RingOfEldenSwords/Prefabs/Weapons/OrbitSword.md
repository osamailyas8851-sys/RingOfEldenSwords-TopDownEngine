# OrbitSword Prefab — Setup Guide

**Prefab path:** `Assets/RingOfEldenSwords/Prefabs/Weapons/OrbitSword.prefab`

---

## Prefab Structure

```
OrbitSword                       (root — born active, m_IsActive: 1)
├── SpriteRenderer               ← renders the sword sprite
├── OrbitWeaponCombat (script)   ← all combat logic: clashing, damage, health
│
└── BladeHitbox                  (child)
    └── BoxCollider2D            ← trigger — the actual hit detection area
        (rotated 45° on Z to align with the blade tip)
```

### Why two GameObjects?

The root holds the **sprite and combat logic**. The child `BladeHitbox` holds the **collider**, rotated 45° to align precisely with the blade tip direction rather than the sprite bounding box. This gives accurate hit detection regardless of the orbit angle.

The `Rigidbody2D` is **not in the prefab** — it is added at runtime by `CharacterOrbitWeapons.GetOrCreateWeapon()` and forced to `Kinematic` so physics cannot fling the sword away when the owner moves.

---

## GameObjects and Components

### Root — `OrbitSword`

| Component | Value | Purpose |
|---|---|---|
| **SpriteRenderer** | Sprite: `3.png` (default), SortingOrder: `100` | Renders the sword icon. Sprite is overridden at runtime by `ApplyDefinition()` when a `WeaponDefinition` is assigned. |
| **OrbitWeaponCombat** | See script section below | All combat behaviour: clash detection, entity damage, health, destruction. |
| **Layer** | `23` | Physics layer controlling which objects this sword can collide with. Set in Project Settings → Physics 2D → Layer Collision Matrix. |
| **Tag** | Set at runtime (`"Player"` or `"Enemy"`) | Faction tag assigned by `CharacterOrbitWeapons` at spawn. Determines who this sword damages. **Do not set a tag in the prefab** — it is always overwritten at runtime. |

### Child — `BladeHitbox`

| Component | Value | Purpose |
|---|---|---|
| **BoxCollider2D** | `Is Trigger: true`, Size: `(1.6, 0.1)` | Thin elongated trigger aligned with the blade. Fires `OnTriggerEnter2D` on the root's `OrbitWeaponCombat`. |
| **Local Rotation Z** | `45°` | Rotates the collider to align with the diagonal blade direction. |
| **Layer** | `23` | Must match the root layer so the collision matrix applies to it too. |

> **Do not add a Rigidbody2D to `BladeHitbox`.** Triggers on child objects work via the parent's Rigidbody2D which is added at runtime by `CharacterOrbitWeapons`.

---

## Scripts

### OrbitWeaponCombat
**Path:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponCombat.cs`
**Base class:** `MonoBehaviour`

All combat logic for one orbiting sword instance. Lives on the root, reads collisions from `BladeHitbox` (child trigger events bubble up to the first Rigidbody2D in the hierarchy).

#### Inspector Fields (default values in prefab)

| Field | Default | Description |
|---|---|---|
| `MaxHealth` | `10` | HP of this sword. Depleted by enemy sword clashes. At 0 → sword is destroyed. **Overridden by `WeaponDefinition` at runtime.** |
| `ClashDamage` | `10` | Damage dealt to an opposing sword on collision. **Overridden by `WeaponDefinition`.** |
| `EntityDamage` | `10` | Damage dealt to a character's `Health` component on contact. **Overridden by `WeaponDefinition`.** |
| `ClashCooldown` | `0.1` | Minimum seconds between clash damage ticks. Prevents one collision registering multiple times. |
| `EntityHitCooldown` | `0.5` | Minimum seconds between entity damage ticks. Prevents rapid-fire damage on sustained contact. |

> **These values are fallbacks only.** When `CharacterOrbitWeapons` has a `WeaponDefinition` assigned, `ApplyDefinition()` overwrites all of them immediately after the sword spawns. The prefab values are only used if `WeaponDefinition` is null.

#### Runtime State

| Property | Description |
|---|---|
| `CurrentHealth` | Current HP — decreases on clash, resets on `ResetHealth()` or `ApplyDefinition()` |
| `IsAlive` | `true` if `CurrentHealth > 0` and state is `Active` |

#### Key Methods

**`ApplyDefinition(OrbitWeaponDefinition def)`**
Called by `CharacterOrbitWeapons.GetOrCreateWeapon()` immediately after the sword is spawned or retrieved from pool.
- Overwrites `MaxHealth`, `ClashDamage`, `EntityDamage`, `ClashCooldown`, `EntityHitCooldown`
- Calls `ResetHealth()` so the sword starts at full HP for the new definition
- Sets `_spriteRenderer.sprite` to `def.Sprite`

**`TakeDamage(float amount)`**
Called by opposing swords during a clash. Reduces `CurrentHealth`. If it reaches 0, calls `DestroyWeapon()`.

**`ResetHealth()`**
Resets `CurrentHealth` to `MaxHealth` and state to `Active`. Called on `OnEnable()` (pool reuse) and inside `ApplyDefinition()`.

**`SetSprite(Sprite sprite)` / `GetSprite()`**
Direct sprite accessors — used when you want to set the sprite without a full definition swap.

**`SetStats(...)`**
Lets you override individual stats without a full `OrbitWeaponDefinition`. Useful for temporary buffs.

#### Key Events

| Event | When fired | Who listens |
|---|---|---|
| `OnDestroyed` | When `CurrentHealth` reaches 0 | `CharacterOrbitWeapons.HandleWeaponDestroyed()` — removes sword from active list, fires `OnWeaponDestroyed`, turns off owner's ImmuneToDamage if last sword |
| `OnClash` | Each time this sword clashes with an opposing sword | Available for external listeners (VFX, audio, etc.) |

#### Collision Logic (`OnTriggerEnter2D`)

When `BladeHitbox` overlaps something, `OnTriggerEnter2D` fires on `OrbitWeaponCombat`:

```
OnTriggerEnter2D(other)
  ├── IsAlive? No → return (dead swords don't hit)
  ├── Same GameObject? → return (self-collision guard)
  ├── Child of self? → return (BladeHitbox hitting parent guard)
  │
  ├── Does other have OrbitWeaponCombat with a DIFFERENT tag?
  │     Yes → PerformClash(otherSword)
  │             ├── both swords take damage
  │             └── both start clash cooldown
  │
  └── Does other have Health AND _canHitEntity is true?
        Yes → CanDamageCharacter(health)?
                ├── Player sword + Enemy tag → damage
                ├── Enemy sword + Player tag → damage
                └── DamageCharacter(health) → health.Damage() + entity cooldown
```

#### Faction Rules

Faction is determined by the **GameObject tag** set by `CharacterOrbitWeapons` at spawn:

| Tag | Damages |
|---|---|
| `"Player"` | Characters tagged `"Enemy"` only |
| `"Enemy"` | Characters tagged `"Player"` only |

Swords with the **same tag never damage each other** — only opposing factions clash. This matches TDE's tag convention used everywhere else in the engine.

---

## ScriptableObject — OrbitWeaponDefinition
**Path:** `Assets/RingOfEldenSwords/Scripts/Combat/Weapons/OrbitWeaponDefinition.cs`
**Assets:** `Assets/RingOfEldenSwords/ScriptableObjects/Weapons/Swords/`

This is the **data layer** for the sword. The prefab is a dumb shell — the SO provides all personality.

| Field | Type | What it overrides on the prefab |
|---|---|---|
| `WeaponName` | string | Used in debug logs only |
| `Sprite` | Sprite | `SpriteRenderer.sprite` on root |
| `MaxHealth` | float | `OrbitWeaponCombat.MaxHealth` |
| `ClashDamage` | float | `OrbitWeaponCombat.ClashDamage` |
| `EntityDamage` | int | `OrbitWeaponCombat.EntityDamage` |
| `ClashCooldown` | float | `OrbitWeaponCombat.ClashCooldown` |
| `EntityHitCooldown` | float | `OrbitWeaponCombat.EntityHitCooldown` |

**48 pre-made definitions** in `Assets/RingOfEldenSwords/ScriptableObjects/Weapons/Swords/`:
- `Sword_Level_1` → Health/Clash/Entity: `10`
- Each level adds `+5` to all three stats
- `Sword_Level_48` → Health/Clash/Entity: `245`

---

## Workflow — How This Prefab Works With Other Scripts

```
1. ENEMY/PLAYER SPAWNS
   CharacterOrbitWeapons.Initialization()
     └── GetOrCreatePivot() → finds/creates "OrbitPivot" child

   CharacterOrbitWeapons.UpdateWeapons(count)
     └── for each sword slot:
           GetOrCreateWeapon(spawnAngle, targetAngle)
             ├── pool empty? → Instantiate(OrbitSword.prefab)
             │   pool has sword? → Dequeue + reuse
             ├── SetParent(OrbitPivot)       ← sword follows pivot rotation
             ├── weapon.tag = "Player"/"Enemy"  ← faction assigned here
             ├── Rigidbody2D added (Kinematic)   ← added at runtime, not in prefab
             ├── ApplyDefinition(WeaponDefinition)
             │     ├── MaxHealth/ClashDamage/EntityDamage set from SO
             │     └── sprite set from SO
             ├── wb.OnDestroyed += HandleWeaponDestroyed
             └── SetActive(true)

2. SWORD ORBITS
   CharacterOrbitWeapons.ProcessAbility() (every frame)
     └── OrbitPivot.Rotate(0, 0, OrbitSpeed × deltaTime)
           └── all child swords rotate with it automatically

3. SWORDS CLASH
   BladeHitbox.OnTriggerEnter2D (enemy sword overlaps player sword)
     └── OrbitWeaponCombat.OnTriggerEnter2D
           └── PerformClash(otherSword)
                 ├── otherSword.TakeDamage(this.ClashDamage)
                 ├── this.TakeDamage(other.ClashDamage)
                 └── both start ClashCooldown

4. SWORD DESTROYED (health reaches 0)
   OrbitWeaponCombat.DestroyWeapon()
     ├── OnDestroyed.Invoke(gameObject) → CharacterOrbitWeapons.HandleWeaponDestroyed()
     │     ├── removes from _activeWeapons list
     │     └── if last sword → ImmuneToDamage = false (shield off)
     └── Destroy(gameObject)  ← removed from pool permanently

5. PLAYER PICKS UP ENEMY LOOT
   OrbitWeaponPickup.Pick(player)
     ├── orbit.WeaponDefinition = Sword_Level_5  ← player upgrades sword type
     └── orbit.AddWeapons(count)
           └── GetOrCreateWeapon() called for each new sword
                 └── ApplyDefinition(Sword_Level_5)  ← new swords have enemy's type
```

---

## Quick Reference — Important Rules

| Rule | Reason |
|---|---|
| **Never set a Tag on the prefab** | Tag is always set to `"Player"` or `"Enemy"` at runtime by `CharacterOrbitWeapons` based on owner type. A prefab tag would be overwritten immediately. |
| **Never add Rigidbody2D to the prefab** | Added at runtime as `Kinematic`. A pre-existing Dynamic Rigidbody2D would let physics fling swords away when the owner moves. |
| **Do not rename `BladeHitbox`** | It is found by component (`BoxCollider2D`) not by name, but keeping the name makes the hierarchy readable. |
| **Prefab stats are fallbacks only** | `ApplyDefinition()` overwrites everything. Only matters if `WeaponDefinition` is null on the owner. |
| **One universal prefab, unlimited types** | Never create a new prefab per sword type. Create a new `OrbitWeaponDefinition` asset instead. |
