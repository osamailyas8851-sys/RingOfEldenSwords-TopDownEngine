using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Per-card button on the Level Select screen.
    /// Populated by LevelSelectScreen at runtime, loads the target scene on click.
    /// Sets <see cref="LevelSelectConfig.CurrentDifficulty"/> from the player's
    /// progression before transitioning so gameplay systems know what tier to run.
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Level Select Button")]
    public class LevelSelectButton : TopDownMonoBehaviour
    {
        [Header("Card Elements")]
        [SerializeField] protected Image      _iconImage;
        [SerializeField] protected TMP_Text   _nameText;
        [SerializeField] protected TMP_Text   _descriptionText;
        [SerializeField] protected TMP_Text   _wavesText;
        [SerializeField] protected TMP_Text   _difficultyText;
        [SerializeField] protected TMP_Text   _progressionText;
        [SerializeField] protected Image      _accentStrip;

        protected LevelData _data;
        protected Button    _button;

        protected virtual void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(OnClicked);
        }

        protected virtual void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClicked);
        }

        /// <summary>
        /// Fill the card UI with data from a LevelData asset.
        /// Called by LevelSelectScreen during its Start().
        /// </summary>
        public virtual void Populate(LevelData data)
        {
            _data = data;

            if (_iconImage != null && data.Icon != null)
                _iconImage.sprite = data.Icon;
            if (_nameText != null)
                _nameText.text = data.LevelName;
            if (_descriptionText != null)
                _descriptionText.text = data.Description;
            if (_wavesText != null)
                _wavesText.text = $"{data.WaveCount} Waves";
            if (_accentStrip != null)
                _accentStrip.color = data.AccentColor;

            // Show progression and current difficulty from saved progress
            UpdateProgressDisplay();
        }

        /// <summary>
        /// Reads saved progress and updates the difficulty and progression text.
        /// </summary>
        public virtual void UpdateProgressDisplay()
        {
            if (_data == null) return;

            int progression    = 0;
            int nextDifficulty = 1;
            bool unlocked      = true;

            if (GameProgressManager.Instance != null)
            {
                LevelProgressEntry entry = GameProgressManager.Instance.GetLevelProgress(_data.LevelName);
                if (entry != null)
                {
                    progression    = entry.Progression;
                    nextDifficulty = GameProgressManager.Instance.GetNextDifficulty(_data.LevelName);
                    unlocked       = entry.Unlocked;
                }
            }

            if (_difficultyText != null)
            {
                if (progression >= 10)
                    _difficultyText.text = "Mastered";
                else
                    _difficultyText.text = $"Difficulty {nextDifficulty}/10";
            }

            if (_progressionText != null)
                _progressionText.text = $"{progression}/10";

            // Dim card if locked
            if (_button != null)
                _button.interactable = unlocked;
        }

        /// <summary>
        /// Stores the selected level and its current difficulty, then transitions.
        /// </summary>
        protected virtual void OnClicked()
        {
            if (_data == null) return;

            LevelSelectConfig.SelectedLevel = _data;

            // Set the difficulty tier the player is about to face
            if (GameProgressManager.Instance != null)
                LevelSelectConfig.CurrentDifficulty = GameProgressManager.Instance.GetNextDifficulty(_data.LevelName);
            else
                LevelSelectConfig.CurrentDifficulty = 1;

            Debug.Log($"[LevelSelectButton] Selected '{_data.LevelName}' at difficulty {LevelSelectConfig.CurrentDifficulty}");

            MMSceneLoadingManager.LoadScene(_data.SceneName);
        }
    }
}
