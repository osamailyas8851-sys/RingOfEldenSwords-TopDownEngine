using UnityEngine;
using TMPro;
using RingOfEldenSwords.Combat.Weapons;
using RingOfEldenSwords.Character.Abilities;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Extends PickableItem with weapon data support for the orbit weapon pickup system.
    ///
    /// LootExtended calls Initialize() on this component immediately after the pooled
    /// pickup is retrieved, passing the WeaponDefinition and WeaponCount read from
    /// CharacterWeaponsOrbit on the dead enemy. When a player then walks into the
    /// pickup, Pick() uses that data to grant the correct weapons.
    ///
    /// Add this component to OrbitWeaponPicker.prefab in place of (or alongside)
    /// any other PickableItem-derived component.
    /// </summary>
    public class PickableItemExtended : PickableItem
    {
        // ─── Inspector References ─────────────────────────────────────────────────

        [Header("Weapon Pickup Visuals")]

        /// SpriteRenderer on the Weapon child — displays the weapon's sprite.
        [Tooltip("SpriteRenderer on the Weapon child GameObject. Assign in Inspector.")]
        [SerializeField] protected SpriteRenderer _weaponSpriteRenderer;

        /// TextMeshPro on the Count child — displays how many weapons this pickup grants.
        [Tooltip("TextMeshPro on the Count child GameObject. Assign in Inspector.")]
        [SerializeField] protected TextMeshPro _countText;

        // ─── Runtime Data (set by LootExtended before activation) ─────────────────

        /// the weapon definition passed from the dead enemy's CharacterWeaponsOrbit.
        /// Null if the enemy had no WeaponDefinition assigned.
        protected OrbitWeaponDefinition _weaponDefinition;

        /// how many weapons this pickup grants, taken from CharacterWeaponsOrbit.WeaponCount
        protected int _weaponCount;

        // ─── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called by LootExtended.ApplyWeaponDataToPickup() immediately after this
        /// object is retrieved from the pool, before it is positioned or activated.
        /// Stores weapon data and immediately updates the visible sprite and count label.
        /// </summary>
        public virtual void Initialize(
            OrbitWeaponDefinition definition,
            int count)
        {
            _weaponDefinition = definition;
            _weaponCount      = count;

            // Re-enable collider for pool reuse — PickableItem.PickItem() disables
            // it when DisableColliderOnPick is true, and nothing re-enables it when
            // the object is reactivated from the pool.
            if (_collider2D != null) _collider2D.enabled = true;
            if (_collider   != null) _collider.enabled   = true;

            ApplyVisuals();
        }

        /// <summary>
        /// Pushes the current weapon data into the prefab's visible elements:
        ///   - _weaponSpriteRenderer.sprite ← definition.Sprite
        ///   - _countText.text              ← count as string
        /// Safe to call with null definition (leaves sprite unchanged).
        /// </summary>
        protected virtual void ApplyVisuals()
        {
            if (_weaponSpriteRenderer != null && _weaponDefinition != null)
                _weaponSpriteRenderer.sprite = _weaponDefinition.Sprite;

            if (_countText != null)
                _countText.SetText("+ {0}", _weaponCount);
        }

        // ─── PickableItem Overrides ───────────────────────────────────────────────

        /// <summary>
        /// Called by PickItem() when a valid picker touches the collider.
        /// Override here to grant the orbit weapons to the picker's
        /// CharacterWeaponsOrbit ability using the data set by Setup().
        /// </summary>
        protected override void Pick(GameObject picker)
        {
            base.Pick(picker);

            var orbit = picker.GetComponent<CharacterWeaponsOrbit>();
            if (orbit == null) return;

            orbit.AddWeapons(_weaponCount, _weaponDefinition);
        }
    }
}
