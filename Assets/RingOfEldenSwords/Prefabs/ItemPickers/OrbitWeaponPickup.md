# OrbitWeaponPickup Prefab — Setup Guide

**Prefab path:** `Assets/RingOfEldenSwords/Prefabs/ItemPickers/OrbitWeaponPickup.prefab`

---

## Prefab Structure

```
OrbitWeaponPickup                (root — born inactive, m_IsActive: 0)
├── SortingGroup                 ← sets sorting layer for all children
├── SpriteRenderer               ← displays the sword sprite icon
├── Rigidbody2D                  ← kinematic, no gravity
├── CircleCollider2D             ← trigger — detects player overlap
├── OrbitWeaponPickup (script)   ← main pickup logic
│
├── Background                   (child)
│   └── SpriteRenderer           ← white semi-transparent square backdrop
│
└── CountText                    (child)
    ├── MeshRenderer             ← renders TMP text geometry
    └── TextMeshPro              ← shows "x3" style count badge
```

### Why the root is born inactive (`m_IsActive: 0`)

The prefab is instantiated as an **inactive** child of the enemy at spawn time.
If it were born active, its `CircleCollider2D` would fire `OnTriggerEnter2D` in the same physics frame as `Instantiate()` — colliding with the nearby player and triggering a pickup before the enemy even dies.

The root stays inactive until `EnemyOrbitLoot.HandleDeath()` calls `SetActive(true)` after the enemy dies, at which point the pickup is already detached and scattered in the world.

---

## GameObjects and Components

### Root — `OrbitWeaponPickup`

| Component | Purpose |
|---|---|
| **SortingGroup** | Sets `Characters` sorting layer (ID `47421343`, layer 4, order 200) for the root and all children. Children use relative sort orders so you only need to configure the layer once here. |
| **SpriteRenderer** | Shows the sword sprite. Default sprite: `3.png` (sword icon). Updated at runtime by `Init()` with the enemy's `WeaponDefinition.Sprite`. |
| **Rigidbody2D** | `Body Type: Kinematic`, `Gravity Scale: 0`. Required for 2D trigger detection. Kinematic so the pickup doesn't fly around from physics forces. |
| **CircleCollider2D** | `Is Trigger: true`. Detects when the player overlaps the pickup. Radius sized to match the visible sprite. |
| **OrbitWeaponPickup** | The pickup script (see Scripts section below). |

### Child — `Background`

| Component | Value | Purpose |
|---|---|---|
| **SpriteRenderer** | Unity built-in white square sprite | Semi-transparent backdrop behind the sword icon |
| `m_Color` | `(1, 1, 1, 0.35)` | 35% opacity white — subtle background only |
| `m_SortingOrder` | `0` (relative to SortingGroup) | Renders behind sword sprite |

> To change background shape: replace the sprite on this SpriteRenderer.
> To change size: adjust the `Scale` of the `Background` GameObject.

### Child — `CountText`

| Component | Value | Purpose |
|---|---|---|
| **TextMeshPro** | Text: `+1`, orthographic mode | Shows how many swords this pickup grants |
| **MeshRenderer** | `m_SortingOrder: 2` (relative to SortingGroup) | Renders on top of sword sprite |
| `m_isOrthographic` | `1` | Required for correct rendering in 2D/orthographic camera |

Count text is hidden (`SetActive(false)`) when count equals 1. Shown as `x2`, `x3`, etc. for counts above 1. This is set by `Init()` at runtime.

---

## Scripts

### OrbitWeaponPickup
**Path:** `Assets/RingOfEldenSwords/Scripts/Combat/Pickups/OrbitWeaponPickup.cs`
**Base class:** TDE `PickableItem`

The main script on this prefab. Extends TDE's `PickableItem` so all collision detection, player-type validation, feedbacks, and disable-on-pick are handled by the base class automatically.

#### Serialized Fields (Inspector)

| Field | Type | Description |
|---|---|---|
| `_weaponSprite` | SpriteRenderer | The SpriteRenderer on the root that shows the sword icon. **Must be wired in the prefab** — auto-wire fallback is disabled because `GetComponentInChildren` would find `Background`'s SpriteRenderer instead. |
| `_countText` | TextMeshPro | The TMP on `CountText` child. Auto-wired in `Awake()` if not set in Inspector. |

#### Inherited from TDE PickableItem (Inspector)

| Field | Value | Description |
|---|---|---|
| `RequirePlayerType` | `true` | Only characters of type `Player` can pick this up. Enemies are ignored. |
| `DisableObjectOnPick` | `true` | Deactivates this GameObject after it is picked up. |
| `DisableObjectOnPickDelay` | `0` | No delay — disappears immediately on pickup. |

#### Runtime Properties (read-only, set by Init)

| Property | Type | Description |
|---|---|---|
| `PickupCount` | `int` | How many weapons this pickup grants. Default: `1`. Set by `Init()`. |
| `WeaponDefinition` | `OrbitWeaponDefinition` | The weapon type to grant the player. Set by `Init()`. Passed to the player's `CharacterOrbitWeapons` on pick. |

#### Key Methods

**`Init(int count, OrbitWeaponDefinition definition)`**
Called by `EnemyOrbitLoot.StampPickup()` before the pickup is activated.
- Sets `PickupCount` and `WeaponDefinition`
- Updates `_weaponSprite.sprite` to `definition.Sprite`
- Updates `_countText.text` to `"x{count}"` and hides it if count is 1

**`Pick(GameObject picker)` (override)**
Called by `PickableItem.PickItem()` after TDE validates the picker is a Player.
- Finds `CharacterOrbitWeapons` on the picker (walks up the hierarchy)
- Sets `orbit.WeaponDefinition = WeaponDefinition` — upgrades the player to this sword type
- Calls `orbit.AddWeapons(PickupCount)` — spawns new swords with the correct definition applied

**`Awake()`**
Runs at `Instantiate()` time even while the object is inactive. Auto-wires `_countText` if not set in Inspector.

---

### PickableItem (TDE base class — inherited)
**Path:** `Assets/TopDownEngine/Common/Scripts/Items/PickableItem.cs`

You do not modify this. It provides:
- `OnTriggerEnter2D` — fires when a collider overlaps the `CircleCollider2D`
- `PickItem(GameObject picker)` — validates character type, calls `Pick()`, triggers feedbacks, disables the object
- `CheckIfPickable()` — returns false if already picked or wrong character type

---

## Sorting Layer Setup

The `SortingGroup` on the root is set to:
- **Sorting Layer:** `Characters` (ID `47421343`, layer index 4)
- **Order in Layer:** `200`

All children use relative sort orders within that group:

| Child | Relative Order | Renders |
|---|---|---|
| Background SpriteRenderer | 0 | Bottom — behind everything |
| Root SpriteRenderer (sword icon) | 1 | Middle — on top of background |
| CountText MeshRenderer | 2 | Top — on top of sword icon |

This means the pickup renders at the same level as the player and enemies (`Characters` layer), never hidden behind them.

> **To change the overall render depth:** adjust the `SortingGroup` order on the root only. Children automatically follow.

---

## Workflow — How This Prefab Works With Other Scripts

```
1. SCENE LOADS
   EnemyOrbitLoot.Start()
     └── Instantiate(OrbitWeaponPickup prefab)
           ├── Born inactive (m_IsActive: 0) → no physics, not visible
           ├── Parented to enemy root
           └── Awake() runs → _countText auto-wired if needed

2. ENEMY DIES
   Health.OnDeath → EnemyOrbitLoot.HandleDeath()
     ├── StampPickup()
     │     ├── Reads CharacterOrbitWeapons.WeaponDefinition (e.g. Sword_Level_5)
     │     ├── Reads CharacterOrbitWeapons.WeaponCount (e.g. 6)
     │     └── Calls pickup.Init(6, Sword_Level_5)
     │           ├── _weaponSprite.sprite = Sword_Level_5.Sprite  (correct sword shown)
     │           ├── _countText.text = "x6"
     │           └── _countText.SetActive(true)   (count > 1)
     ├── pickup.transform.SetParent(null)     → detached from enemy
     ├── pickup.transform.position = deathPos + scatter
     └── pickup.SetActive(true)              → now visible, physics active

3. PLAYER OVERLAPS PICKUP
   CircleCollider2D.OnTriggerEnter2D (via PickableItem)
     └── PickItem(player)
           ├── CheckIfPickable() → passes (RequirePlayerType = true, picker is Player)
           └── Pick(player)
                 ├── orbit = player.GetComponentInParent<CharacterOrbitWeapons>()
                 ├── orbit.WeaponDefinition = Sword_Level_5   ← player upgrades sword type
                 └── orbit.AddWeapons(6)
                       └── 6 new swords spawn, each gets ApplyDefinition(Sword_Level_5)
                             → correct sprite, MaxHealth=30, ClashDamage=30, EntityDamage=30

4. AFTER PICKUP
   DisableObjectOnPick = true → pickup GameObject deactivated immediately
```

---

## Important Notes

| Rule | Reason |
|---|---|
| **Do not set `m_IsActive: 1` on the root in the prefab** | Born-active prefabs fire `OnTriggerEnter2D` at `Instantiate()` time, corrupting the player's sword count before the enemy dies |
| **`_weaponSprite` must be wired in the prefab Inspector** | No auto-wire fallback — `GetComponentInChildren<SpriteRenderer>` would find `Background` first |
| **Do not remove `RequirePlayerType`** | Without it, enemy characters could trigger pickup, causing undefined behaviour |
| **`SortingGroup` is the single place to change the rendering layer** | All children inherit from it automatically |
