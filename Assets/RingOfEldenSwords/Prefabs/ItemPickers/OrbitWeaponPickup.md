# OrbitWeaponPickup Prefab — Setup Guide

**Prefab path:** `Assets/RingOfEldenSwords/Prefabs/ItemPickers/OrbitWeaponPickup.prefab`

---

## Overview

`OrbitWeaponPickup` is the world item that appears when an enemy dies. It shows the enemy's sword
sprite and a count badge, and grants the player that sword type + count when collected.

**Architecture:** The pickup is **embedded as an inactive child inside the enemy prefab** — it is
never instantiated at runtime. This is the foundation of the zero-lag loot drop optimization
(see the Optimization section in `Enemy.md` for full detail).

---

## Prefab Structure

```
OrbitWeaponPickup        (root — born inactive, m_IsActive: 0)
├── SortingGroup         ← sets sorting layer for all children at once
├── SpriteRenderer       ← displays the sword sprite icon
├── Rigidbody2D          ← kinematic, no gravity
├── CircleCollider2D     ← trigger — detects player overlap
├── OrbitWeaponPickup (script)
│
├── Background           (child)
│   └── SpriteRenderer   ← semi-transparent square backdrop behind the icon
│
└── CountText            (child)
    ├── MeshRenderer     ← renders TMP text geometry
    └── TextMeshPro      ← shows "x3" style count badge
```

### Why the root must be born inactive (`m_IsActive: 0`)

The pickup is an inactive child of the enemy prefab. If it were born active, its `CircleCollider2D`
would fire `OnTriggerEnter2D` immediately at scene load (or when near the player), triggering a
pickup before the enemy ever dies.

The root stays inactive until `EnemyOrbitLoot.HandleDeath()` calls `SetActive(true)` — at which
point the pickup is already detached from the enemy and scattered into the world.

> **Do not set `m_IsActive: 1` on the root in the prefab.** This will break the entire loot system.

---

## GameObjects and Components

### Root — `OrbitWeaponPickup`

| Component             | Purpose |
|-----------------------|---------|
| **SortingGroup**      | Sets `Characters` sorting layer (layer 4, order 200) for the root and all children. Change render depth here only — children inherit automatically. |
| **SpriteRenderer**    | Shows the sword sprite. Default sprite: `3.png` (sword icon). Updated at runtime by `Init()` with the enemy's `WeaponDefinition.Sprite`. |
| **Rigidbody2D**       | `Body Type: Kinematic`, `Gravity Scale: 0`. Required for 2D trigger detection. Kinematic so the pickup stays in place. |
| **CircleCollider2D**  | `Is Trigger: true`. Detects when the player overlaps the pickup. |
| **OrbitWeaponPickup** | The pickup script (see Scripts section below). |

### Child — `Background`

| Component         | Value              | Purpose |
|-------------------|--------------------|---------|
| **SpriteRenderer**| Unity white square | Semi-transparent backdrop behind the sword icon |
| `m_Color`         | `(1, 1, 1, 0.35)`  | 35% opacity — subtle background only |
| `m_SortingOrder`  | `0` (relative)     | Renders behind sword sprite |

> To change background shape: replace the sprite on this SpriteRenderer.
> To change size: adjust the `Scale` of the `Background` GameObject.

### Child — `CountText`

| Component       | Value              | Purpose |
|-----------------|--------------------|---------|
| **TextMeshPro** | `+1`, orthographic | Shows how many swords this pickup grants |
| **MeshRenderer**| `m_SortingOrder: 2`| Renders on top of sword sprite |
| `m_isOrthographic` | `1`             | Required for correct rendering in 2D/orthographic camera |

Count text is hidden (`SetActive(false)`) when count equals 1.
Shown as `x2`, `x3`, etc. for counts above 1. Set by `Init()` at runtime.

---

## Scripts

### OrbitWeaponPickup
**Path:** `Assets/RingOfEldenSwords/Scripts/Combat/Pickups/OrbitWeaponPickup.cs`
**Base class:** TDE `PickableItem`

Extends TDE's `PickableItem` — collision detection, player-type validation, feedbacks, and
disable-on-pick are all handled by the base class automatically.

#### Serialized Fields (Inspector)

| Field            | Type            | Description |
|------------------|-----------------|-------------|
| `_weaponSprite`  | SpriteRenderer  | The SpriteRenderer on the root showing the sword icon. **Must be wired in the Inspector** — auto-wire is disabled because `GetComponentInChildren<SpriteRenderer>` would find `Background`'s renderer first. |
| `_countText`     | TextMeshPro     | TMP on the `CountText` child. Auto-wired in `Awake()` via `GetComponentInChildren<TextMeshPro>(true)` if not set. |

#### Inherited from TDE PickableItem (Inspector)

| Field                        | Value  | Description |
|------------------------------|--------|-------------|
| `RequirePlayerType`          | `true` | Only characters of type `Player` can pick this up. Enemies are ignored. |
| `DisableObjectOnPick`        | `true` | Deactivates this GameObject after it is picked up. |
| `DisableObjectOnPickDelay`   | `0`    | No delay — disappears immediately on pickup. |

#### Runtime Properties (set by Init, read-only after that)

| Property            | Type                   | Description |
|---------------------|------------------------|-------------|
| `PickupCount`       | `int`                  | How many weapons this pickup grants. Default: `1`. Set by `Init()`. |
| `WeaponDefinition`  | `OrbitWeaponDefinition`| The weapon type to grant the player. Set by `Init()`. |

#### Key Methods

**`Init(int count, OrbitWeaponDefinition definition)`**

Called by `EnemyOrbitLoot.StampPickup()` before the pickup is ever activated. Safe to call while
the object is inactive.

- Sets `PickupCount` and `WeaponDefinition`
- Updates `_weaponSprite.sprite` to `definition.Sprite`
- Sets `_countText.text = "x{count}"` and hides it when count is 1

**`Pick(GameObject picker)` (override)**

Called by `PickableItem.PickItem()` after TDE validates the picker is a Player.

- Finds `CharacterOrbitWeapons` on the picker (walks up the hierarchy)
- Sets `orbit.WeaponDefinition = WeaponDefinition` — upgrades the player to this sword type
- Calls `orbit.AddWeapons(PickupCount)` — spawns new swords with the definition applied

**`Awake()`**

Runs even while inactive (Unity calls `Awake()` on all objects in a prefab when it is loaded,
regardless of active state). Auto-wires `_countText` if not set in Inspector so `Init()` can
safely call `_countText.text = ...` before `SetActive(true)`.

---

### PickableItem (TDE base class — inherited)
**Path:** `Assets/TopDownEngine/Common/Scripts/Items/PickableItem.cs`

Do not modify. Provides:
- `OnTriggerEnter2D` — fires when a collider overlaps the `CircleCollider2D`
- `PickItem(GameObject picker)` — validates character type, calls `Pick()`, triggers feedbacks, disables object
- `CheckIfPickable()` — returns false if already picked or wrong character type

> **Note on `PickableItem.Start()`:** This is the method that triggers `MMFeedbacks.Initialization()`
> the first time the pickup becomes active. It only runs once. The `EnemyOrbitLoot` pre-warm
> coroutine forces this to run at scene load time so death-frame activation is instant.
> See `Enemy.md → Optimization: Zero-Lag Loot Drops` for full detail.

---

## Sorting Layer Setup

The `SortingGroup` on the root is set to:
- **Sorting Layer:** `Characters` (ID `47421343`, layer index 4)
- **Order in Layer:** `200`

All children use relative sort orders within that group:

| Child                        | Relative Order | Renders |
|------------------------------|----------------|---------|
| Background SpriteRenderer    | 0              | Bottom — behind everything |
| Root SpriteRenderer (sword)  | 1              | Middle — on top of background |
| CountText MeshRenderer       | 2              | Top — on top of sword icon |

> **To change render depth:** adjust only the `SortingGroup` on the root. Children follow automatically.

---

## Full Lifecycle

```
1. SCENE LOADS
   EnemyPatrolBase prefab is loaded into the scene.
   OrbitWeaponPickup is already embedded as an inactive child.

   Awake() runs (even while inactive):
     └── _countText auto-wired via GetComponentInChildren<TextMeshPro>(true)

   EnemyOrbitLoot.Start():
     ├── StampPickup() → pickup.Init(WeaponCount, WeaponDefinition)
     │     ├── _weaponSprite.sprite = WeaponDefinition.Sprite  (correct sword set)
     │     ├── _countText.text = "x3"
     │     └── _countText.SetActive(count > 1)
     └── StartCoroutine(PreWarmPickup())
           ├── Frame 0: pickup.SetActive(true)    ← PickableItem.Start() queued
           ├── [frame completes] ← PickableItem.Start() runs, MMFeedbacks initialized
           └── Frame 1: pickup.SetActive(false)   ← hidden again, fully warmed

2. ENEMY DIES
   Health.OnDeath → EnemyOrbitLoot.HandleDeath()
     ├── _warmed = true                          (blocks coroutine from re-hiding)
     ├── pickup.SetParent(null)                  (detached — survives enemy disable)
     ├── pickup.position = deathPos + scatter
     └── pickup.SetActive(true)                  ← INSTANT, no lag, Start already ran

3. PLAYER OVERLAPS PICKUP
   CircleCollider2D.OnTriggerEnter2D → PickableItem.PickItem(player)
     └── OrbitWeaponPickup.Pick(player)
           ├── orbit.WeaponDefinition = WeaponDefinition  ← player upgrades sword type
           └── orbit.AddWeapons(PickupCount)
                 └── new swords spawn with correct sprite + stats

4. AFTER PICKUP
   DisableObjectOnPick = true → TDE deactivates pickup immediately

5. ENEMY RESPAWN (if applicable)
   EnemyOrbitLoot.OnRespawn()
     ├── pickup.SetActive(false)
     ├── pickup.SetParent(enemy root)     ← re-embedded as child
     └── StampPickup()                    ← re-stamped for next death cycle
         (SetActive(true) next death is still instant — Start() never runs again)
```

---

## Important Rules

| Rule | Reason |
|------|--------|
| **`m_IsActive` on root must be `0`** | Born-active prefab fires trigger at scene load, corrupting player sword count |
| **`_weaponSprite` must be wired in Inspector** | `GetComponentInChildren<SpriteRenderer>` finds `Background` first — wrong renderer |
| **Do not remove `RequirePlayerType`** | Without it, enemy characters trigger the pickup |
| **Do not add a `Destroy()` or `Deactivate()` call after pickup** | TDE's `DisableObjectOnPick` handles this. Double-deactivation is harmless but redundant |
| **`SortingGroup` is the only place to change render layer** | Children inherit automatically |
| **Do not call `Init()` after `SetActive(true)`** | `Init()` must be called while inactive — calling it while active skips `_countText.gameObject.SetActive` for count=1 case due to ordering |
