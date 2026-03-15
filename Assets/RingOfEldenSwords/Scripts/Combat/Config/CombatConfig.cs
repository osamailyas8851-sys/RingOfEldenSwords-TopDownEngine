using UnityEngine;

namespace RingOfEldenSwords.Combat.Config
{
    [CreateAssetMenu(fileName = "CombatConfig", menuName = "RingOfEldenSwords/CombatConfig")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Orbit Settings")]
        public float orbitRadius = 2f;
        public float orbitSpeed = 180f;
        public float spawnAngleOffset = -45f;
        public float arrivalDuration = 0.5f;
        public AnimationCurve sweepCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Weapon Stats")]
        public float weaponMaxHealth = 10f;
        public float clashDamage = 10f;
        public int entityDamage = 10;
    }
}
