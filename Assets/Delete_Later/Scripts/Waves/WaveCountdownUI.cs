using TMPro;
using UnityEngine;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Drives a wave counter (X/Total) and a countdown timer TMP text. The text
    /// objects must be placed under the HUD canvas in the scene and wired here;
    /// this component does not build its own UI.
    /// </summary>
    [AddComponentMenu("TopDown Engine/UI/Wave Countdown UI")]
    public class WaveCountdownUI : TopDownMonoBehaviour
    {
        [Header("References")]
        /// the wave manager to listen to. If null, auto-finds one in the scene
        [Tooltip("the wave manager to listen to. If null, auto-finds one in the scene")]
        public WaveManager TargetWaveManager;

        /// TMP text that displays the wave counter, e.g. "1/16"
        [Tooltip("TMP text that displays the wave counter, e.g. \"1/16\"")]
        public TMP_Text WaveCounterText;

        /// TMP text that displays the countdown, e.g. "00:59"
        [Tooltip("TMP text that displays the countdown, e.g. \"00:59\"")]
        public TMP_Text CountdownText;

        [Header("Display")]
        /// format string for the wave counter. {0}=waves passed, {1}=total
        [Tooltip("format string for the wave counter. {0}=waves passed, {1}=total")]
        public string WaveCounterFormat = "{0}/{1}";

        /// format string for the countdown. {0}=minutes, {1}=seconds
        [Tooltip("format string for the countdown. {0}=minutes, {1}=seconds")]
        public string CountdownFormat = "{0:00}:{1:00}";

        /// shown in place of the countdown when all waves are done
        [Tooltip("shown in place of the countdown when all waves are done")]
        public string ClearedText = "CLEAR";

        protected virtual void Start()
        {
            if (TargetWaveManager == null) TargetWaveManager = FindObjectOfType<WaveManager>();
            if (TargetWaveManager == null)
            {
                Debug.LogWarning("[WaveCountdownUI] No WaveManager found in scene.");
                return;
            }

            TargetWaveManager.OnCountdownTick += HandleTick;
            TargetWaveManager.OnWaveAdvanced += HandleWaveAdvanced;
            TargetWaveManager.OnAllWavesCompleted += HandleCompleted;

            UpdateCounter(TargetWaveManager.WavesPassed, TargetWaveManager.TotalWaves);
            UpdateCountdown(TargetWaveManager.WaveInterval);
        }

        protected virtual void OnDestroy()
        {
            if (TargetWaveManager != null)
            {
                TargetWaveManager.OnCountdownTick -= HandleTick;
                TargetWaveManager.OnWaveAdvanced -= HandleWaveAdvanced;
                TargetWaveManager.OnAllWavesCompleted -= HandleCompleted;
            }
        }

        protected virtual void HandleTick(float secondsRemaining, int wavesPassed)
        {
            UpdateCounter(wavesPassed, TargetWaveManager.TotalWaves);
            UpdateCountdown(secondsRemaining);
        }

        protected virtual void HandleWaveAdvanced(int wavesPassed, int total)
        {
            UpdateCounter(wavesPassed, total);
        }

        protected virtual void HandleCompleted()
        {
            if (CountdownText != null) CountdownText.text = ClearedText;
            if (TargetWaveManager != null) UpdateCounter(TargetWaveManager.TotalWaves, TargetWaveManager.TotalWaves);
        }

        protected virtual void UpdateCounter(int wavesPassed, int total)
        {
            if (WaveCounterText == null) return;
            WaveCounterText.text = string.Format(WaveCounterFormat, wavesPassed, total);
        }

        protected virtual void UpdateCountdown(float secondsRemaining)
        {
            if (CountdownText == null) return;
            int mins = Mathf.FloorToInt(secondsRemaining / 60f);
            int secs = Mathf.FloorToInt(secondsRemaining % 60f);
            CountdownText.text = string.Format(CountdownFormat, mins, secs);
        }
    }
}
