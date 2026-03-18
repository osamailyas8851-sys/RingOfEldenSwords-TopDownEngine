using UnityEngine;
using MoreMountains.TopDownEngine;
using RingOfEldenSwords.Combat.Orbit;
using RingOfEldenSwords.Combat.Pickups;

namespace RingOfEldenSwords
{
    /// <summary>
    /// Replaces TDE's Loot component.
    /// On enemy death: reads OrbitSystem sword count, spawns WeaponPickup
    /// with the correct sword count and sword sprite.
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
                Debug.LogError("[EnemyLootDropper] pickupPrefab not assigned!");
                return;
            }

            // Read sword count from OrbitSystem
            OrbitSystem orbit = GetComponentInChildren<OrbitSystem>(includeInactive: true);
            int swordCount = orbit != null ? Mathf.Max(1, orbit.WeaponCount) : 1;

            // Get the sword sprite from the first active sword
            Sprite swordSprite = GetSwordSprite(orbit);

            // Scatter position
            Vector2 scatter = Random.insideUnitCircle.normalized * Random.Range(minScatter, maxScatter);
            Vector3 spawnPos = transform.position + new Vector3(scatter.x, scatter.y, 0);

            // Spawn and initialise
            GameObject go = Instantiate(pickupPrefab, spawnPos, Quaternion.identity);
            WeaponPickup pickup = go.GetComponent<WeaponPickup>();
            if (pickup != null)
                pickup.Init(swordCount, swordSprite);
        }

        private Sprite GetSwordSprite(OrbitSystem orbit)
        {
            if (orbit == null) return null;

            // Try to get sprite from first active sword in orbit
            foreach (GameObject sword in orbit.Weapons)
            {
                if (sword == null) continue;
                SpriteRenderer sr = sword.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                    return sr.sprite;
            }

            // Fall back to OrbitSword prefab sprite via AssetDatabase in editor
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
