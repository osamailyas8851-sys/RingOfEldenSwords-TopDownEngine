using UnityEngine;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Combat.Orbit;

namespace RingOfEldenSwords.Combat.Pickups
{
    /// <summary>
    /// Dropped by enemies on death. Player walks over it to gain a sword in their orbit.
    /// Uses TDE's built-in trigger detection via OnTriggerEnter2D.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WeaponPickup : MonoBehaviour
    {
        [Header("Bob Animation")]
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float bobSpeed = 2f;

        private Vector3 _startPos;
        private Collider2D _col;

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            _col.isTrigger = true;
        }

        void OnEnable()
        {
            _startPos = transform.position;
        }

        void Update()
        {
            // Simple bob up/down
            float y = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // Look for CharacterOrbitWeapons on the colliding object
            CharacterOrbitWeapons orbit = other.GetComponentInParent<CharacterOrbitWeapons>();
            if (orbit == null) return;

            // Add 1 sword to player orbit
            orbit.AddWeapons(1);

            Debug.Log("[WeaponPickup] Player collected a sword!");
            Destroy(gameObject);
        }
    }
}
