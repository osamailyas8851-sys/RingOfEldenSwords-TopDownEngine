using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Add to the player character alongside Health.
    /// Listens for XPGainEvent (fired by EnemyXPReward on enemy death),
    /// tracks XP and Level, and updates the HUD XP bar + level text.
    ///
    /// HUD binding: at Start() the script searches for an MMProgressBar
    /// whose GameObject is named "XPBar" and a Text named "LevelText".
    /// </summary>
    [AddComponentMenu("TopDown Engine/Character/XP/Player XP")]
    public class PlayerXP : TopDownMonoBehaviour, MMEventListener<XPGainEvent>
    {
        [Header("XP Settings")]
        [Tooltip("XP required to level up. Resets to 0 after each level.")]
        public float MaxXP = 100f;

        [Header("State (read-only)")]
        [MMReadOnly]
        public float CurrentXP;
        [MMReadOnly]
        public int CurrentLevel;

        // ── HUD references (found at runtime) ──────────────────────────────
        protected MMProgressBar _xpBar;
        protected Text          _levelText;
        protected TMP_Text      _levelTextTMP;

        // ── Unity Lifecycle ─────────────────────────────────────────────────

        protected virtual void Start()
        {
            FindHUDReferences();
            UpdateXPBar();
            UpdateLevelText();
        }

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<XPGainEvent>();
        }

        protected virtual void OnDisable()
        {
            this.MMEventStopListening<XPGainEvent>();
        }

        // ── Event Listener ──────────────────────────────────────────────────

        public virtual void OnMMEvent(XPGainEvent xpEvent)
        {
            GainXP(xpEvent.XPAmount);
        }

        // ── Public API ──────────────────────────────────────────────────────

        /// <summary>
        /// Adds XP and handles level-ups. Safe to call from anywhere.
        /// </summary>
        public virtual void GainXP(int amount)
        {
            CurrentXP += amount;

            while (CurrentXP >= MaxXP)
            {
                CurrentXP -= MaxXP;
                CurrentLevel++;
            }

            UpdateXPBar();
            UpdateLevelText();
        }

        // ── HUD Updates ─────────────────────────────────────────────────────

        protected virtual void UpdateXPBar()
        {
            if (_xpBar != null)
                _xpBar.UpdateBar(CurrentXP, 0f, MaxXP);
        }

        protected virtual void UpdateLevelText()
        {
            string label = "Lv. " + CurrentLevel;
            if (_levelText != null)    _levelText.text    = label;
            if (_levelTextTMP != null) _levelTextTMP.text = label;
        }

        // ── HUD Binding ─────────────────────────────────────────────────────

        /// <summary>
        /// Finds the XP bar and level text in the scene by name convention.
        /// Called once in Start() — the HUD Canvas must already exist.
        /// </summary>
        protected virtual void FindHUDReferences()
        {
            // Find XP bar by name
            MMProgressBar[] bars = FindObjectsByType<MMProgressBar>(FindObjectsSortMode.None);
            foreach (MMProgressBar bar in bars)
            {
                if (bar.gameObject.name == "XPBar")
                {
                    _xpBar = bar;
                    break;
                }
            }

            // Find level text by name — supports both legacy Text and TextMeshPro
            GameObject levelTextGO = GameObject.Find("LevelText");
            if (levelTextGO != null)
            {
                _levelText    = levelTextGO.GetComponent<Text>();
                _levelTextTMP = levelTextGO.GetComponent<TMP_Text>();
            }

            if (_xpBar == null)
                Debug.LogWarning("[PlayerXP] No GameObject named 'XPBar' with MMProgressBar found in scene. " +
                                 "XP bar will not update.", this);
            if (_levelText == null && _levelTextTMP == null)
                Debug.LogWarning("[PlayerXP] No GameObject named 'LevelText' with Text or TMP_Text found in scene. " +
                                 "Level text will not update.", this);
        }
    }
}
