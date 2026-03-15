using UnityEngine;
using MoreMountains.TopDownEngine;

namespace RingOfEldenSwords
{
    /// <summary>
    /// Spawns a fixed number of enemies evenly spread across the floor at game start.
    /// No waves, no timers — just place enemies once when the scene loads.
    /// </summary>
    public class GameStartEnemySpawner : MonoBehaviour
    {
        [Header("Enemy Settings")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private int enemyCount = 5;

        [Header("Spawn Area")]
        [Tooltip("Centre of the spawn area in world space")]
        [SerializeField] private Vector2 areaCenter = Vector2.zero;
        [Tooltip("Half-width and half-height of the spawn area")]
        [SerializeField] private Vector2 areaExtents = new Vector2(8f, 6f);

        [Header("Safety")]
        [Tooltip("Enemies won't spawn closer than this to the player spawn point")]
        [SerializeField] private float minDistanceFromCenter = 3f;

        void Start()
        {
            SpawnEnemies();
        }

        private void SpawnEnemies()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("[GameStartEnemySpawner] Enemy prefab not assigned!");
                return;
            }

            // Divide the area into a grid and pick one point per cell
            // so enemies are evenly spread rather than clustered
            Vector2[] positions = GetEvenlySpreadPositions(enemyCount);

            foreach (Vector2 pos in positions)
            {
                GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);

                // Let TDE's Health system know it's alive
                Health health = enemy.GetComponent<Health>();
                if (health != null)
                    health.Revive();
            }

            Debug.Log($"[GameStartEnemySpawner] Spawned {enemyCount} enemies.");
        }

        /// <summary>
        /// Returns evenly spread positions by dividing the area into a grid.
        /// Each enemy gets one grid cell and spawns near its centre.
        /// </summary>
        private Vector2[] GetEvenlySpreadPositions(int count)
        {
            Vector2[] positions = new Vector2[count];

            // Work out grid dimensions — e.g. 5 enemies → 3 cols x 2 rows (or similar)
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);

            float cellW = (areaExtents.x * 2f) / cols;
            float cellH = (areaExtents.y * 2f) / rows;

            int index = 0;
            for (int r = 0; r < rows && index < count; r++)
            {
                for (int c = 0; c < cols && index < count; c++)
                {
                    // Centre of this grid cell
                    float x = areaCenter.x - areaExtents.x + cellW * c + cellW * 0.5f;
                    float y = areaCenter.y - areaExtents.y + cellH * r + cellH * 0.5f;

                    Vector2 candidate = new Vector2(x, y);

                    // Push away from player spawn if too close
                    if (candidate.magnitude < minDistanceFromCenter)
                    {
                        Vector2 dir = candidate.normalized;
                        if (dir == Vector2.zero) dir = Vector2.right;
                        candidate = dir * minDistanceFromCenter;
                    }

                    positions[index++] = candidate;
                }
            }

            return positions;
        }

        // Draw spawn area in Scene view for easy tweaking
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(areaCenter, areaExtents * 2f);

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(areaCenter, minDistanceFromCenter);
        }
    }
}
