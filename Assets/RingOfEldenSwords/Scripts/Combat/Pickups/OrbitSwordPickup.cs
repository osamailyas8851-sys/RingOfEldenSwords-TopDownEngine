using UnityEngine;
using TMPro;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;

namespace RingOfEldenSwords.Combat.Pickups
{
    /// <summary>
    /// A pickable item dropped by enemies on death.
    /// Displays the enemy's sword sprite and count.
    /// Extends TDE's PickableItem so collision, player-type gating,
    /// feedbacks and disable-on-pick are all handled by the base class.
    /// </summary>
    [AddComponentMenu("RingOfEldenSwords/Items/Orbit Sword Pickup")]
    public class OrbitSwordPickup : PickableItem
    {

protected override void Start()
        {
            base.Start();
            // Auto-wire if not assigned in Inspector
            if (_swordSprite == null)
                _swordSprite = GetComponent<SpriteRenderer>();
            if (_countText == null)
                _countText = GetComponentInChildren<TextMeshPro>();
        }

        [Header("Orbit Sword Pickup")]
        [Tooltip("SpriteRenderer on this GameObject that shows the sword sprite.")]
        [SerializeField] private SpriteRenderer _swordSprite;

        [Tooltip("TextMeshPro component that shows the count badge (e.g. 'x3').")]
        [SerializeField] private TextMeshPro _countText;

        /// <summary>How many swords this pickup grants when collected.</summary>
        public int SwordCount { get; private set; } = 1;

        // ── Initialisation ────────────────────────────────────────────────────

        /// <summary>
        /// Called by EnemyOrbitLoot immediately after the pickup is instantiated
        /// (and again after enemy respawn) to stamp in the correct sword count and sprite.
        /// </summary>
        public void Init(int count, Sprite sprite)
        {
            SwordCount = count;

            if (_swordSprite != null)
                _swordSprite.sprite = sprite;

            if (_countText != null)
            {
                _countText.text = $"x{count}";
                _countText.gameObject.SetActive(count > 1);
            }
        }

        // ── PickableItem override ─────────────────────────────────────────────

        /// <summary>
        /// Called by PickableItem.PickItem() after all validation passes.
        /// Adds swords to the picking character's orbit ring.
        /// </summary>
        protected override void Pick(GameObject picker)
        {
            // Walk up to find CharacterOrbitWeapons (picker may be a collider child)
            CharacterOrbitWeapons orbit = picker.GetComponentInParent<CharacterOrbitWeapons>();
            if (orbit == null)
                orbit = picker.GetComponent<CharacterOrbitWeapons>();

            orbit?.AddWeapons(SwordCount);
        }
    }
}
