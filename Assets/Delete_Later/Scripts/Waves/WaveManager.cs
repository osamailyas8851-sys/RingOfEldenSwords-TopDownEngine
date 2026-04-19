using System;
using System.Collections;
using UnityEngine;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Minimal wave timing controller. Counts up a wave index from 0..TotalWaves,
    /// advancing by one every WaveInterval seconds. No spawning yet — this exists
    /// only to drive the on-screen countdown and wave counter UI.
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

        // ── Public read-only state ─────────────────────────────────────────

        /// how many waves have passed so far (0 before the first tick completes)
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

        // ── Lifecycle ──────────────────────────────────────────────────────

        protected virtual void Start()
        {
            _runRoutine = StartCoroutine(RunWaves());
        }

        protected virtual IEnumerator RunWaves()
        {
            WavesPassed = 0;

            while (WavesPassed < TotalWaves)
            {
                TimeUntilNextWave = WaveInterval;
                while (TimeUntilNextWave > 0f)
                {
                    OnCountdownTick?.Invoke(TimeUntilNextWave, WavesPassed);
                    TimeUntilNextWave -= Time.deltaTime;
                    yield return null;
                }

                WavesPassed++;
                OnWaveAdvanced?.Invoke(WavesPassed, TotalWaves);
            }

            TimeUntilNextWave = 0f;
            OnAllWavesCompleted?.Invoke();
        }
    }
}
