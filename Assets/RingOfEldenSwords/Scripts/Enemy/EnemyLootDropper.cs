using UnityEngine;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Character.Abilities;
using RingOfEldenSwords.Combat.Pickups;

namespace RingOfEldenSwords
{
    /// <summary>
    /// On enemy death: reads CharacterOrbitWeapons sword count, spawns a
    /// WeaponPickup scattered away from the death position with the correct
    /// sword count and sprite.
    /// </summary>
    public class EnemyLootDropper : MonoBehaviour
    {
        [Header("Pickup")]
        [SerializeField] private GameObject pickupPrefab;

        [Header("Scatter")]
        [SerializeField] private float minScatter = 0.8f;
        [SerializeField] private float maxScatter = 1.8f;

        private Health _health;

        void Awake()
        {
            _health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (_health != null)
                _health.OnDeath += HandleDeath;
        }

        void OnDisable()
        {
            if (_health != null)
                _health.OnDeath -= HandleDeath;
        }

        private void HandleDeath()
        {
            if (pickupPrefab == null)
            {
                Debug.LogError("[EnemyLootDropper] pickupPrefab not assigned!", this);
                return;
            }

            // Read sword count from CharacterOrbitWeapons on the enemy root
            CharacterOrbitWeapons orbit = GetComponent<CharacterOrbitWeapons>();
            int swordCount = orbit != null ? Mathf.Max(1, orbit.WeaponCount) : 1;

            // Get sprite from first active sword in the orbit ring
            Sprite swordSprite = GetSwordSprite(orbit);

            // Scatter position — spawn away from enemy so pickup is always visible
            Vector2 scatter = Random.insideUnitCircle.normalized * Random.Range(minScatter, maxScatter);
            Vector3 spawnPos = transform.position + new Vector3(scatter.x, scatter.y, 0f);

            // Spawn and initialise the pickup
            GameObject go = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
            WeaponPickup pickup = go.GetComponent<WeaponPickup>();
            if (pickup != null)
                pickup.Init(swordCount, swordSprite);
        }

        private Sprite GetSwordSprite(CharacterOrbitWeapons orbit)
        {
            if (orbit == null) return null;

            // Try to get sprite from the first active sword
            foreach (GameObject sword in orbit.Weapons)
            {
                if (sword == null) continue;
                SpriteRenderer sr = sword.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                    return sr.sprite;
            }

            // Fallback: load sprite directly from the OrbitSword prefab asset
            #if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/RingOfEldenSwords/Prefabs/Weapons/OrbitSword.prefab");
            if (prefab != null)
            {
                var sr = prefab.GetComponent<SpriteRenderer>();
                if (sr != null) return sr.sprite;
            }
            #endif

            return null;
        }
    }
}
