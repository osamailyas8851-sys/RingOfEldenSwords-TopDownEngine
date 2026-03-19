using UnityEngine;
using TMPro;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.Combat.Pickups
{
    /// <summary>
    /// A pickable item dropped by enemies on death.
    /// Displays the enemy's weapon sprite and count.
    /// Extends TDE's PickableItem so collision, player-type gating,
    /// feedbacks and disable-on-pick are all handled by the base class.
    /// </summary>
    [AddComponentMenu("RingOfEldenSwords/Items/Orbit Weapon Pickup")]
    public class OrbitWeaponPickup : PickableItem
    {
        [Header("Orbit Weapon Pickup")]
        [Tooltip("SpriteRenderer on this GameObject that shows the weapon sprite.")]
        [SerializeField] private SpriteRenderer _weaponSprite;

        [Tooltip("TextMeshPro component that shows the count badge (e.g. 'x3').")]
        [SerializeField] private TextMeshPro _countText;

        /// <summary>How many weapons this pickup grants when collected.</summary>
        public int PickupCount { get; private set; } = 1;

        /// <summary>
        /// The weapon definition stamped by the enemy at spawn.
        /// Passed to the player's CharacterOrbitWeapons on pick so they
        /// receive the correct sword type, not just a count increase.
        /// </summary>
        public OrbitWeaponDefinition WeaponDefinition { get; private set; }

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        protected void Awake()
        {
            // Wire in Awake so references are valid even when the object is
            // still inactive — Init() is called before SetActive(true).
            // includeInactive=true on all calls so inactive children are found too.
            // _weaponSprite must be wired in the prefab Inspector — no auto-wire fallback
            // because GetComponentInChildren would find the Background SR instead
            if (_countText == null)
                _countText = GetComponentInChildren<TextMeshPro>(true);
        }

        // ── Initialisation ────────────────────────────────────────────────────

        /// <summary>
        /// Called by EnemyOrbitLoot immediately after the pickup is instantiated
        /// to stamp in the correct weapon definition, count, and sprite.
        /// </summary>
        public void Init(int count, OrbitWeaponDefinition definition)
        {
            PickupCount      = count;
            WeaponDefinition = definition;

            // Resolve sprite: definition is the source of truth, fall back to existing
            Sprite sprite = (definition != null) ? definition.Sprite : null;

            Debug.Log($"[OrbitWeaponPickup] Init called — count={count} " +
                      $"def={definition?.WeaponName ?? "NULL"} " +
                      $"sprite={sprite?.name ?? "NULL"} " +
                      $"_weaponSprite={(_weaponSprite != null ? _weaponSprite.gameObject.name : "NULL")}");

            if (_weaponSprite != null && sprite != null)
                _weaponSprite.sprite = sprite;

            if (_countText != null)
            {
                _countText.text = $"x{count}";
                _countText.gameObject.SetActive(count > 1);
            }
        }

        // ── PickableItem override ─────────────────────────────────────────────

        /// <summary>
        /// Called by PickableItem.PickItem() after all validation passes.
        /// Adds weapons to the picking character's orbit ring AND upgrades
        /// their WeaponDefinition to match the dropped sword type.
        /// </summary>
        protected override void Pick(GameObject picker)
        {
            CharacterOrbitWeapons orbit = picker.GetComponentInParent<CharacterOrbitWeapons>();
            if (orbit == null)
                orbit = picker.GetComponent<CharacterOrbitWeapons>();

            if (orbit == null) return;

            // Upgrade the player's weapon definition to the dropped sword type
            if (WeaponDefinition != null)
                orbit.WeaponDefinition = WeaponDefinition;

            orbit.AddWeapons(PickupCount);
        }
    }
}
