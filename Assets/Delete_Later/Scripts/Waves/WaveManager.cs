using System;
using System.Collections;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Counts up a wave index 0..TotalWaves, advancing by one every WaveInterval
    /// seconds. When a wave ticks over, spawns EnemyPrefab enemies evenly across
    /// a rectangular spawn area (keeping a safety radius around the player spawn).
    /// When all waves are survived, fires the TDE LevelComplete event.
    /// </summary>
    [AddComponentMenu("TopDown Engine/Spawn/Wave Manager")]
    public class WaveManager : TopDownMonoBehaviour
    {
        [Header("Wave Settings")]
        /// total number of waves in the level
        [Tooltip("total number of waves in the level")]
        public int TotalWaves = 16;

        /// seconds between consecutive waves (also the countdown length)
        [Tooltip("seconds between consecutive waves (also the countdown length)")]
        public float WaveInterval = 60f;

        /// if true, spawns wave 1 immediately on Start instead of waiting a full interval
        [Tooltip("if true, spawns wave 1 immediately on Start instead of waiting a full interval")]
        public bool SpawnFirstWaveImmediately = true;

        [Header("Enemy Settings")]
        /// the enemy prefab to instantiate each wave
        [Tooltip("the enemy prefab to instantiate each wave")]
        public GameObject EnemyPrefab;

        /// number of enemies in wave 1
        [Tooltip("number of enemies in wave 1")]
        public int BaseEnemiesPerWave = 5;

        /// extra enemies added per wave index (wave N spawns Base + Scaling*(N-1))
        [Tooltip("extra enemies added per wave index (wave N spawns Base + Scaling*(N-1))")]
        public int WaveCountScaling = 1;

        [Header("Spawn Area")]
        /// centre of the spawn area in world space
        [Tooltip("centre of the spawn area in world space")]
        public Vector2 AreaCenter = Vector2.zero;

        /// half-width and half-height of the spawn rectangle
        [Tooltip("half-width and half-height of the spawn rectangle")]
        public Vector2 AreaExtents = new Vector2(8f, 6f);

        /// enemies won't spawn closer than this to the area centre (player spawn)
        [Tooltip("enemies won't spawn closer than this to the area centre (player spawn)")]
        public float MinDistanceFromCenter = 3f;

        [Header("Debug")]
        [MMInspectorButton("ForceSpawnNextWave")]
        public bool ForceSpawnNextWaveBtn;

        // ── Public read-only state ─────────────────────────────────────────

        /// how many waves have passed so far (0 before the first wave spawns)
        public virtual int WavesPassed { get; protected set; }

        /// seconds remaining until the next wave ticks over
        public virtual float TimeUntilNextWave { get; protected set; }

        /// fires every frame while the countdown is active. Args: seconds remaining, waves passed so far
        public event Action<float, int> OnCountdownTick;

        /// fires once a wave ticks over. Args: new WavesPassed value, TotalWaves
        public event Action<int, int> OnWaveAdvanced;

        /// fires once when all waves are completed
        public event Action OnAllWavesCompleted;

        protected Coroutine _runRoutine;
        protected bool _forceNextWave;

        // ── Lifecycle ──────────────────────────────────────────────────────

        protected virtual void Start()
        {
            _runRoutine = StartCoroutine(RunWaves());
        }

        protected virtual IEnumerator RunWaves()
        {
            if (EnemyPrefab == null)
            {
                Debug.LogError("[WaveManager] EnemyPrefab is not assigned — disabling.");
                enabled = false;
                yield break;
            }

            WavesPassed = 0;

            for (int i = 1; i <= TotalWaves; i++)
            {
                if (i > 1 || !SpawnFirstWaveImmediately)
                {
                    yield return RunCountdown(WaveInterval);
                }

                SpawnWave(i);

                WavesPassed = i;
                OnWaveAdvanced?.Invoke(WavesPassed, TotalWaves);
            }

            // final survival countdown after the last wave
            yield return RunCountdown(WaveInterval);

            TimeUntilNextWave = 0f;
            OnAllWavesCompleted?.Invoke();
            TopDownEngineEvent.Trigger(TopDownEngineEventTypes.LevelComplete, null);
            MMGameEvent.Trigger("Save");
        }

        protected virtual IEnumerator RunCountdown(float seconds)
        {
            TimeUntilNextWave = seconds;
            while (TimeUntilNextWave > 0f && !_forceNextWave)
            {
                OnCountdownTick?.Invoke(TimeUntilNextWave, WavesPassed);
                TimeUntilNextWave -= Time.deltaTime;
                yield return null;
            }
            _forceNextWave = false;
            TimeUntilNextWave = 0f;
            OnCountdownTick?.Invoke(0f, WavesPassed);
        }

        // ── Spawning ───────────────────────────────────────────────────────

        protected virtual void SpawnWave(int waveIndex)
        {
            int count = BaseEnemiesPerWave + WaveCountScaling * (waveIndex - 1);
            count = Mathf.Max(1, count);

            Vector2[] positions = GetEvenlySpreadPositions(count);
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject enemy = Instantiate(EnemyPrefab, positions[i], Quaternion.identity);
                Health health = enemy.GetComponent<Health>();
                if (health != null) health.Revive();
            }

            Debug.Log($"[WaveManager] Wave {waveIndex}/{TotalWaves} spawned ({count} enemies).");
        }

        protected virtual Vector2[] GetEvenlySpreadPositions(int count)
        {
            Vector2[] positions = new Vector2[count];
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);
            float cellW = (AreaExtents.x * 2f) / cols;
            float cellH = (AreaExtents.y * 2f) / rows;

            int index = 0;
            for (int r = 0; r < rows && index < count; r++)
            {
                for (int c = 0; c < cols && index < count; c++)
                {
                    float x = AreaCenter.x - AreaExtents.x + cellW * c + cellW * 0.5f;
                    float y = AreaCenter.y - AreaExtents.y + cellH * r + cellH * 0.5f;

                    Vector2 candidate = new Vector2(x, y);
                    if ((candidate - AreaCenter).magnitude < MinDistanceFromCenter)
                    {
                        Vector2 dir = (candidate - AreaCenter).normalized;
                        if (dir == Vector2.zero) dir = Vector2.right;
                        candidate = AreaCenter + dir * MinDistanceFromCenter;
                    }
                    positions[index++] = candidate;
                }
            }
            return positions;
        }

        /// <summary>
        /// Inspector debug button — skips the current countdown so the next wave spawns immediately.
        /// </summary>
        public virtual void ForceSpawnNextWave()
        {
            _forceNextWave = true;
        }

        // ── Gizmos ─────────────────────────────────────────────────────────

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(AreaCenter, AreaExtents * 2f);
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(AreaCenter, MinDistanceFromCenter);
        }
    }
}
