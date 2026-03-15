using UnityEngine;

namespace RingOfEldenSwords.Combat.Weapons
{
    /// <summary>
    /// Relay script on BladeHitbox child GameObject.
    /// Forwards trigger events up to parent WeaponBehaviour
    /// so collision detection works when collider is on a child.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BladeHitboxRelay : MonoBehaviour
    {
        private WeaponBehaviour weaponBehaviour;

        void Awake()
        {
            weaponBehaviour = GetComponentInParent<WeaponBehaviour>();
            if (weaponBehaviour == null)
                Debug.LogError($"[BladeHitboxRelay] No WeaponBehaviour found in parent of {gameObject.name}!");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            weaponBehaviour?.HandleHitboxTrigger(other);
        }
    }
}
