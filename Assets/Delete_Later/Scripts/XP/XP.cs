using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Event triggered when an enemy dies, broadcasting its XP value.
    /// </summary>
    public struct XPGainEvent
    {
        public int XPAmount;
        public XPGainEvent(int amount) { XPAmount = amount; }
        static XPGainEvent e;
        public static void Trigger(int amount)
        {
            e.XPAmount = amount;
            MMEventManager.TriggerEvent(e);
        }
    }

    /// <summary>
    /// Event triggered whenever the player's XP or Level changes (for the UI to listen to).
    /// </summary>
    public struct XPChangeEvent
    {
        public int CurrentXP;
        public int MaxXP;
        public int CurrentLevel;
        public bool JustLeveledUp;

        static XPChangeEvent e;
        public static void Trigger(int currentXP, int maxXP, int currentLevel, bool justLeveledUp)
        {
            e.CurrentXP     = currentXP;
            e.MaxXP         = maxXP;
            e.CurrentLevel  = currentLevel;
            e.JustLeveledUp = justLeveledUp;
            MMEventManager.TriggerEvent(e);
        }
    }

    [AddComponentMenu("TopDown Engine/Character/Core/XP")]
    public class XP : TopDownMonoBehaviour, MMEventListener<XPGainEvent>
    {
        [MMInspectorGroup("XP Reward (For Enemies)", true, 1)]
        [Tooltip("The amount of XP given to the player when this character dies.")]
        public int XPReward = 25;

        [MMInspectorGroup("XP Tracking (For Player)", true, 2)]
        [MMReadOnly] public int CurrentXP = 0;
        [Tooltip("XP required to reach the next level. Scales up each level by LevelScalingMultiplier.")]
        public int MaxXP = 100;
        [MMReadOnly] public int CurrentLevel = 1;
        [Tooltip("How much the MaxXP requirement multiplies every time you level up (e.g. 1.2 means 100 → 120 → 144).")]
        public float LevelScalingMultiplier = 1.2f;

        [MMInspectorGroup("Feedbacks", true, 3)]
        [Tooltip("Feedback to play when the player levels up (e.g., particle explosion, sound).")]
        public MMFeedbacks LevelUpFeedbacks;

        protected Health    _health;
        protected Character _character;
        protected bool      _isPlayer;

        protected virtual void Awake()
        {
            Initialization();
        }

        protected virtual void Start()
        {
            // Fire the initial event so XPBarUI syncs on game start without waiting for a kill
            if (_isPlayer)
                XPChangeEvent.Trigger(CurrentXP, MaxXP, CurrentLevel, false);
        }

        protected virtual void Initialization()
        {
            _health    = GetComponent<Health>();
            _character = GetComponent<Character>();
            _isPlayer  = _character != null && _character.CharacterType == Character.CharacterTypes.Player;

            if (_isPlayer)
                LevelUpFeedbacks?.Initialization(this.gameObject);
        }

        protected virtual void OnEnable()
        {
            // Enemy: listen to own death to fire XP reward
            if (!_isPlayer && _health != null)
                _health.OnDeath += HandleDeath;

            // Player: listen for XP reward events from killed enemies
            if (_isPlayer)
                this.MMEventStartListening<XPGainEvent>();
        }

        protected virtual void OnDisable()
        {
            if (!_isPlayer && _health != null)
                _health.OnDeath -= HandleDeath;

            if (_isPlayer)
                this.MMEventStopListening<XPGainEvent>();
        }

        /// <summary>
        /// (ENEMY) Fires when this character's Health reaches 0.
        /// </summary>
        protected virtual void HandleDeath()
        {
            if (XPReward > 0)
                XPGainEvent.Trigger(XPReward);
        }

        /// <summary>
        /// (PLAYER) Receives XPGainEvents broadcast by dead enemies.
        /// Only players register for this event so the _isPlayer check is not needed,
        /// but kept as a safety net in case of misuse.
        /// </summary>
        public virtual void OnMMEvent(XPGainEvent xpEvent)
        {
            GainXP(xpEvent.XPAmount);
        }

        /// <summary>
        /// (PLAYER) Adds XP, handles level-ups, and broadcasts the result to the UI.
        /// </summary>
        public virtual void GainXP(int amount)
        {
            if (amount <= 0) return;

            CurrentXP += amount;
            bool leveledUp = false;

            while (CurrentXP >= MaxXP)
            {
                CurrentXP -= MaxXP;
                CurrentLevel++;
                MaxXP     = Mathf.RoundToInt(MaxXP * LevelScalingMultiplier);
                leveledUp = true;
            }

            // Play feedback once after all level-ups are resolved, not once per level
            if (leveledUp)
                LevelUpFeedbacks?.PlayFeedbacks();

            XPChangeEvent.Trigger(CurrentXP, MaxXP, CurrentLevel, leveledUp);
        }
    }
}
