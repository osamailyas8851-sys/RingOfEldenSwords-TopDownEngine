using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    // ── Serializable Data ───────────────────────────────────────────────

    /// <summary>
    /// Per-level completion data. Progression is a 1–10 scale
    /// (1 = just unlocked, 10 = fully mastered).
    /// </summary>
    [System.Serializable]
    public class LevelProgressEntry
    {
        public string LevelName;
        public bool   Unlocked  = false;
        public bool   Completed = false;

        [Range(0, 10)]
        [Tooltip("0 = never played, 1 = just started, 10 = fully mastered.")]
        public int Progression = 0;
    }

    /// <summary>
    /// All persistent player data that survives across app launches.
    /// Serialized to disk via <see cref="MMSaveLoadManager"/>.
    /// </summary>
    [System.Serializable]
    public class GameProgressData
    {
        public int Coins    = 0;
        public int Diamonds = 0;

        [Tooltip("Persistent account level — fully independent of the per-run in-game XP system. " +
                 "Earned via AddAccountXP() (e.g. on level complete).")]
        public int PlayerLevel = 1;

        [Tooltip("Account XP accumulated toward the next PlayerLevel.")]
        public int AccountXP = 0;

        [Tooltip("XP required to reach the next PlayerLevel.")]
        public int AccountXPToNext = 100;

        public List<LevelProgressEntry> Levels = new List<LevelProgressEntry>();
    }

    // ── Events ──────────────────────────────────────────────────────────

    /// <summary>
    /// Broadcast whenever the persistent progress changes (coins, diamonds,
    /// player level, or level progression). UI listens to this.
    /// </summary>
    public struct GameProgressEvent
    {
        public int Coins;
        public int Diamonds;
        public int PlayerLevel;

        static GameProgressEvent e;
        public static void Trigger(int coins, int diamonds, int playerLevel)
        {
            e.Coins       = coins;
            e.Diamonds    = diamonds;
            e.PlayerLevel = playerLevel;
            MMEventManager.TriggerEvent(e);
        }
    }

    // ── Manager ─────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton that owns and persists all cross-session player data
    /// (coins, diamonds, player level, per-level progression).
    ///
    /// Follows the TDE pattern established by <c>DeadlineProgressManager</c>:
    /// <list type="bullet">
    ///   <item><see cref="MMSingleton{T}"/> for DontDestroyOnLoad singleton</item>
    ///   <item><see cref="MMSaveLoadManager"/> for file-based persistence</item>
    ///   <item><see cref="MMEventListener{T}"/> for reacting to engine events</item>
    /// </list>
    ///
    /// Place this on a GameObject in your landing / main-menu scene.
    /// It will persist across all scene loads.
    /// </summary>
    [AddComponentMenu("TopDown Engine/Managers/Game Progress Manager")]
    public class GameProgressManager : MMSingleton<GameProgressManager>,
        MMEventListener<TopDownEngineEvent>
    {
        // ── Inspector ───────────────────────────────────────────────────

        [Header("Levels")]
        [Tooltip("Initial level list. On first launch these become the save data. " +
                 "Level_1 is unlocked by default.")]
        public List<LevelProgressEntry> InitialLevels = new List<LevelProgressEntry>();

        [Header("Account Level")]
        [Tooltip("Account XP awarded each time the player completes a level.")]
        [Min(0)]
        public int AccountXPPerLevelComplete = 50;

        [Tooltip("XP required to reach level 2. Each subsequent level multiplies this by AccountXPGrowth.")]
        [Min(1)]
        public int AccountXPBase = 100;

        [Tooltip("Multiplier applied to AccountXPToNext after each level-up.")]
        [Min(1f)]
        public float AccountXPGrowth = 1.5f;

        [Header("Debug")]
        [MMInspectorButton("ForceSave")]
        public bool ForceSaveBtn;

        [MMInspectorButton("ForceLoad")]
        public bool ForceLoadBtn;

        [MMInspectorButton("ResetAllProgress")]
        public bool ResetProgressBtn;

        // ── Runtime State ───────────────────────────────────────────────

        public virtual GameProgressData Progress { get; protected set; }

        // ── Persistence Constants ───────────────────────────────────────

        protected const string _saveFolderName = "GameProgressData";
        protected const string _saveFileName   = "Progress.data";

        // ── Statics Reset (Enter Play Mode support) ─────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        protected static void InitializeStatics()
        {
            _instance = null;
        }

        // ── Lifecycle ───────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            LoadProgress();
        }

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<TopDownEngineEvent>();
        }

        protected virtual void OnDisable()
        {
            this.MMEventStopListening<TopDownEngineEvent>();
        }

        protected virtual void Start()
        {
            BroadcastProgress();
        }

        // =====================================================================
        //  PUBLIC API
        // =====================================================================

        /// <summary>
        /// Adds coins and saves immediately.
        /// </summary>
        public virtual void AddCoins(int amount)
        {
            Progress.Coins = Mathf.Max(0, Progress.Coins + amount);
            SaveProgress();
            BroadcastProgress();
        }

        /// <summary>
        /// Adds diamonds and saves immediately.
        /// </summary>
        public virtual void AddDiamonds(int amount)
        {
            Progress.Diamonds = Mathf.Max(0, Progress.Diamonds + amount);
            SaveProgress();
            BroadcastProgress();
        }

        /// <summary>
        /// Spends coins if the player has enough. Returns true on success.
        /// </summary>
        public virtual bool SpendCoins(int amount)
        {
            if (Progress.Coins < amount) return false;
            Progress.Coins -= amount;
            SaveProgress();
            BroadcastProgress();
            return true;
        }

        /// <summary>
        /// Spends diamonds if the player has enough. Returns true on success.
        /// </summary>
        public virtual bool SpendDiamonds(int amount)
        {
            if (Progress.Diamonds < amount) return false;
            Progress.Diamonds -= amount;
            SaveProgress();
            BroadcastProgress();
            return true;
        }

        /// <summary>
        /// Adds account XP (persistent, independent of in-game XP system).
        /// Handles level-ups, rollover XP, and saves immediately.
        /// </summary>
        public virtual void AddAccountXP(int amount)
        {
            if (amount <= 0) return;

            Progress.AccountXP += amount;

            // Level up as many times as the accumulated XP allows
            while (Progress.AccountXP >= Progress.AccountXPToNext)
            {
                Progress.AccountXP -= Progress.AccountXPToNext;
                Progress.PlayerLevel += 1;
                Progress.AccountXPToNext = Mathf.Max(1,
                    Mathf.RoundToInt(Progress.AccountXPToNext * AccountXPGrowth));
                Debug.Log($"[GameProgressManager] Account level up! PlayerLevel → {Progress.PlayerLevel}");
            }

            SaveProgress();
            BroadcastProgress();
        }

        /// <summary>
        /// Sets a level's progression value (clamped 1–10) and saves.
        /// Only increases — never downgrades existing progression.
        /// </summary>
        public virtual void SetLevelProgression(string levelName, int progression)
        {
            LevelProgressEntry entry = GetOrCreateLevelEntry(levelName);
            entry.Progression = Mathf.Clamp(Mathf.Max(entry.Progression, progression), 0, 10);
            SaveProgress();
        }

        /// <summary>
        /// Returns the <see cref="LevelProgressEntry"/> for a level, or null.
        /// </summary>
        public virtual LevelProgressEntry GetLevelProgress(string levelName)
        {
            return Progress.Levels.Find(l => l.LevelName == levelName);
        }

        /// <summary>
        /// Returns the next difficulty tier the player should face for this level.
        /// Progression 0 → difficulty 1, Progression 5 → difficulty 6, etc.
        /// Capped at 10. If the level is fully completed, returns 10.
        /// </summary>
        public virtual int GetNextDifficulty(string levelName)
        {
            LevelProgressEntry entry = GetLevelProgress(levelName);
            if (entry == null) return 1;
            return Mathf.Clamp(entry.Progression + 1, 1, 10);
        }

        // =====================================================================
        //  EVENT HANDLERS
        // =====================================================================

        /// <summary>
        /// Reacts to engine-level events (level complete, game over, etc.).
        /// </summary>
        public virtual void OnMMEvent(TopDownEngineEvent engineEvent)
        {
            switch (engineEvent.EventType)
            {
                case TopDownEngineEventTypes.LevelComplete:
                    HandleLevelComplete();
                    break;

                case TopDownEngineEventTypes.GameOver:
                    HandleGameOver();
                    break;
            }
        }

        // =====================================================================
        //  INTERNAL LOGIC
        // =====================================================================

        /// <summary>
        /// Called when the current level is completed.
        /// Each level has 10 difficulty tiers (1–10). The player must clear
        /// all 10 to fully complete the level:
        ///   - Progression 0 = never cleared, next difficulty = 1
        ///   - Progression 5 = cleared tiers 1-5, next difficulty = 6
        ///   - Progression 10 = all tiers cleared → Completed = true, next level unlocked
        ///
        /// Only advances progression if the player beat the correct tier
        /// (prevents replaying easy tiers to farm progression).
        /// </summary>
        protected virtual void HandleLevelComplete()
        {
            string currentScene = SceneManager.GetActiveScene().name;

            string levelName = LevelSelectConfig.HasSelection
                ? LevelSelectConfig.SelectedLevel.LevelName
                : currentScene;

            int difficultyPlayed = LevelSelectConfig.CurrentDifficulty;

            LevelProgressEntry entry = GetOrCreateLevelEntry(levelName);
            entry.Unlocked = true;

            // Only advance if the player beat the next required tier
            int nextRequiredDifficulty = entry.Progression + 1;
            if (difficultyPlayed >= nextRequiredDifficulty && entry.Progression < 10)
            {
                entry.Progression = Mathf.Clamp(entry.Progression + 1, 0, 10);
                Debug.Log($"[GameProgressManager] '{levelName}' progression → {entry.Progression}/10 " +
                          $"(cleared difficulty {difficultyPlayed})");
            }

            // Fully completed (all 10 tiers cleared) → unlock next level
            if (entry.Progression >= 10)
            {
                entry.Completed = true;

                int idx = Progress.Levels.IndexOf(entry);
                if (idx >= 0 && idx < Progress.Levels.Count - 1)
                {
                    Progress.Levels[idx + 1].Unlocked = true;
                    Debug.Log($"[GameProgressManager] Unlocked '{Progress.Levels[idx + 1].LevelName}'");
                }
            }

            // Award account XP (scaled by difficulty so harder tiers give more)
            int xpAward = AccountXPPerLevelComplete * Mathf.Max(1, difficultyPlayed);
            AddAccountXP(xpAward);

            SaveProgress();
            BroadcastProgress();
        }

        /// <summary>
        /// Called on game over. Saves current state (XP, coins earned so far).
        /// </summary>
        protected virtual void HandleGameOver()
        {
            SaveProgress();
        }

        // =====================================================================
        //  PERSISTENCE (MMSaveLoadManager)
        // =====================================================================

        /// <summary>
        /// Saves current progress to disk.
        /// </summary>
        public virtual void SaveProgress()
        {
            MMSaveLoadManager.Save(Progress, _saveFileName, _saveFolderName);
        }

        /// <summary>
        /// Alias for inspector button.
        /// </summary>
        protected virtual void ForceSave() => SaveProgress();

        /// <summary>
        /// Loads progress from disk. If no save file exists, creates fresh
        /// progress from <see cref="InitialLevels"/>.
        /// </summary>
        public virtual void LoadProgress()
        {
            GameProgressData loaded = (GameProgressData)MMSaveLoadManager.Load(
                typeof(GameProgressData), _saveFileName, _saveFolderName);

            if (loaded != null)
            {
                Progress = loaded;
                Debug.Log($"[GameProgressManager] Loaded progress — Coins:{Progress.Coins} " +
                          $"Diamonds:{Progress.Diamonds} PlayerLevel:{Progress.PlayerLevel} " +
                          $"Levels:{Progress.Levels.Count}");
            }
            else
            {
                Progress = CreateInitialProgress();
                SaveProgress(); // Persist immediately so next launch finds the file
                Debug.Log("[GameProgressManager] No save found — created fresh progress.");
            }
        }

        /// <summary>
        /// Alias for inspector button.
        /// </summary>
        protected virtual void ForceLoad() => LoadProgress();

        /// <summary>
        /// Creates a fresh <see cref="GameProgressData"/> from the
        /// <see cref="InitialLevels"/> list configured in the Inspector.
        /// </summary>
        protected virtual GameProgressData CreateInitialProgress()
        {
            GameProgressData fresh = new GameProgressData();

            foreach (LevelProgressEntry template in InitialLevels)
            {
                fresh.Levels.Add(new LevelProgressEntry
                {
                    LevelName   = template.LevelName,
                    Unlocked    = template.Unlocked,
                    Completed   = false,
                    Progression = 0
                });
            }

            // Ensure first level is always unlocked
            if (fresh.Levels.Count > 0)
                fresh.Levels[0].Unlocked = true;

            return fresh;
        }

        /// <summary>
        /// Deletes the save folder, wiping all progress.
        /// </summary>
        public virtual void ResetAllProgress()
        {
            MMSaveLoadManager.DeleteSaveFolder(_saveFolderName);
            Progress = CreateInitialProgress();
            SaveProgress();
            BroadcastProgress();
            Debug.Log("[GameProgressManager] All progress reset.");
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================

        /// <summary>
        /// Finds an existing entry by level name, or creates a new locked one.
        /// </summary>
        protected virtual LevelProgressEntry GetOrCreateLevelEntry(string levelName)
        {
            LevelProgressEntry entry = Progress.Levels.Find(l => l.LevelName == levelName);
            if (entry == null)
            {
                entry = new LevelProgressEntry { LevelName = levelName, Unlocked = true };
                Progress.Levels.Add(entry);
            }
            return entry;
        }

        /// <summary>
        /// Fires a <see cref="GameProgressEvent"/> so UI can update.
        /// </summary>
        protected virtual void BroadcastProgress()
        {
            GameProgressEvent.Trigger(Progress.Coins, Progress.Diamonds, Progress.PlayerLevel);
        }
    }
}
