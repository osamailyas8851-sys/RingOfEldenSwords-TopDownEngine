using UnityEngine;
using TMPro;
using RingOfEldenSwords.Combat.Orbit;

namespace RingOfEldenSwords.Combat.Pickups
{
    /// <summary>
    /// Dropped by enemies on death.
    /// Shows the sword sprite + a count badge (how many swords the enemy had).
    /// Player walks over it to gain that many swords.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class WeaponPickup : MonoBehaviour
    {
        [Header("Bob Animation")]
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float bobSpeed = 3f;

        [Header("References")]
        [SerializeField] private SpriteRenderer swordSprite;
        [SerializeField] private TextMeshPro countText;

        // How many swords this pickup gives the player
        private int _swordCount = 1;
        private Vector3 _startPos;

void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            // Auto-wire references if not set in Inspector
            if (swordSprite == null)
                swordSprite = GetComponent<SpriteRenderer>();
            if (countText == null)
                countText = GetComponentInChildren<TextMeshPro>();
        }

        void OnEnable()
        {
            _startPos = transform.position;
        }

        void Update()
        {
            float y = _startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        /// <summary>Called by EnemyLootDropper to set count + sprite before the pickup is visible.</summary>
        public void Init(int swordCount, Sprite sprite)
        {
            _swordCount = Mathf.Max(1, swordCount);

            if (swordSprite != null && sprite != null)
                swordSprite.sprite = sprite;

            if (countText != null)
            {
                countText.text = _swordCount > 1 ? $"x{_swordCount}" : "";
                countText.enabled = _swordCount > 1;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            CharacterOrbitWeapons orbit = other.GetComponentInParent<CharacterOrbitWeapons>();
            if (orbit == null) return;

            orbit.AddWeapons(_swordCount);
            Debug.Log($"[WeaponPickup] Player collected {_swordCount} sword(s)!");
            Destroy(gameObject);
        }
    }
}
