using UnityEngine;
using TMPro;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Weapons;

namespace RingOfEldenSwords.Combat.Pickups
{
    /// <summary>
    /// A pickable item embedded as an inactive child of the enemy prefab.
    /// On enemy death: stamped with weapon data, detached, scattered, activated.
    /// On pickup: re-attached to the original enemy parent as inactive child — ready for reuse.
    /// No Instantiate() or Destroy() ever occurs after scene load.
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
        /// The weapon definition stamped by the enemy at death time.
        /// Passed to the player's CharacterOrbitWeapons on pick.
        /// </summary>
        public OrbitWeaponDefinition WeaponDefinition { get; private set; }

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        protected void Awake()
        {
            // Wire in Awake so references are valid even when the object is
            // still inactive — Init() is called before SetActive(true).
            if (_countText == null)
                _countText = GetComponentInChildren<TextMeshPro>(true);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by EnemyOrbitLoot at death time to stamp weapon data onto this pickup.
        /// </summary>
        public void Init(int count, OrbitWeaponDefinition definition)
        {
            PickupCount      = count;
            WeaponDefinition = definition;

            Sprite sprite = (definition != null) ? definition.Sprite : null;

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
        /// Adds weapons to the picking character's orbit ring, upgrades their
        /// WeaponDefinition, then re-attaches this pickup to the enemy for reuse.
        /// </summary>
        protected override void Pick(GameObject picker)
        {
            CharacterOrbitWeapons orbit = picker.GetComponentInParent<CharacterOrbitWeapons>();
            if (orbit == null)
                orbit = picker.GetComponent<CharacterOrbitWeapons>();

            if (orbit == null) return;

            if (WeaponDefinition != null)
                orbit.WeaponDefinition = WeaponDefinition;

            orbit.AddWeapons(PickupCount);
        }
    }
}
