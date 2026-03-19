using UnityEngine;
using TMPro;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;

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

protected override void Start()
        {
            base.Start();
            // Auto-wire if not assigned in Inspector
            if (_weaponSprite == null)
                _weaponSprite = GetComponent<SpriteRenderer>();
            if (_countText == null)
                _countText = GetComponentInChildren<TextMeshPro>();
        }

        [Header("Orbit Weapon Pickup")]
        [Tooltip("SpriteRenderer on this GameObject that shows the weapon sprite.")]
        [SerializeField] private SpriteRenderer _weaponSprite;

        [Tooltip("TextMeshPro component that shows the count badge (e.g. 'x3').")]
        [SerializeField] private TextMeshPro _countText;

        /// <summary>How many weapons this pickup grants when collected.</summary>
        public int PickupCount { get; private set; } = 1;

        // ── Initialisation ────────────────────────────────────────────────────

        /// <summary>
        /// Called by EnemyOrbitLoot immediately after the pickup is instantiated
        /// (and again after enemy respawn) to stamp in the correct weapon count and sprite.
        /// </summary>
        public void Init(int count, Sprite sprite)
        {
            PickupCount = count;

            if (_weaponSprite != null)
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
        /// Adds weapons to the picking character's orbit ring.
        /// </summary>
        protected override void Pick(GameObject picker)
        {
            // Walk up to find CharacterOrbitWeapons (picker may be a collider child)
            CharacterOrbitWeapons orbit = picker.GetComponentInParent<CharacterOrbitWeapons>();
            if (orbit == null)
                orbit = picker.GetComponent<CharacterOrbitWeapons>();

            orbit?.AddWeapons(PickupCount);
        }
    }
}
