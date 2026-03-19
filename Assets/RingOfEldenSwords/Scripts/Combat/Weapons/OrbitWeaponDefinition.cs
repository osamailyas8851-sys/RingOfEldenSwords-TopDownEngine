using UnityEngine;

namespace RingOfEldenSwords.Combat.Weapons
{
    /// <summary>
    /// Data asset describing a single orbit weapon type.
    /// Create via: right-click in Project → Create → RingOfEldenSwords → Orbit Weapon Definition
    ///
    /// One asset per weapon type (e.g. WpnDef_FireSword, WpnDef_IceSword).
    /// Assign to CharacterOrbitWeapons.WeaponDefinition on each enemy prefab variant.
    /// The universal OrbitSword prefab reads this at spawn time — no prefab variants needed per sword type.
    /// </summary>
    [CreateAssetMenu(
        menuName = "RingOfEldenSwords/Orbit Weapon Definition",
        fileName = "WpnDef_New")]
    public class OrbitWeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Display name shown in editor and debug logs.")]
        public string WeaponName = "Basic Sword";

        [Header("Visuals")]
        [Tooltip("Sprite rendered on the orbiting weapon GameObject.")]
        public Sprite Sprite;

        [Header("Combat Stats")]
        [Tooltip("Health points of the weapon. Reaches 0 → weapon is destroyed by a clash.")]
        public float MaxHealth = 10f;

        [Tooltip("Damage this weapon deals to an opposing weapon when they clash.")]
        public float ClashDamage = 10f;

        [Tooltip("Damage this weapon deals to a character's Health on contact.")]
        public int EntityDamage = 10;

        [Header("Cooldowns")]
        [Tooltip("Minimum seconds between clash damage ticks.")]
        public float ClashCooldown = 0.1f;

        [Tooltip("Minimum seconds between entity damage ticks.")]
        public float EntityHitCooldown = 0.5f;
    }
}
