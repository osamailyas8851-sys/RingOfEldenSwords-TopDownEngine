using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Manager for the Level Select scene.
    /// Populates card slots from a LevelData array and handles the fade-in entrance.
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Level Select Screen")]
    public class LevelSelectScreen : TopDownMonoBehaviour
    {
        [Header("Levels")]
        [Tooltip("Level data assets in display order. Each entry maps to a LevelSelectButton card slot.")]
        [SerializeField] protected LevelData[] Levels;

        [Header("Card Slots")]
        [Tooltip("Button card slots in the UI (same order as Levels array).")]
        [SerializeField] protected LevelSelectButton[] CardSlots;

        [Header("Fade In")]
        [Tooltip("CanvasGroup on the main content panel for a fade-in entrance.")]
        [SerializeField] protected CanvasGroup _contentCanvasGroup;

        [Tooltip("Duration of the fade-in (unscaled time).")]
        [SerializeField] protected float _fadeInDuration = 0.4f;

        [Header("Auto-Load (Resources/)")]
        [Tooltip("Resources sub-folder to auto-load LevelData assets from. Leave empty to use the manual Levels array.")]
        [SerializeField] protected string LevelsResourcePath = "Levels";

        protected virtual void Start()
        {
            LoadLevelsFromResources();
            PopulateCards();
            FadeIn();
        }

        /// <summary>
        /// Auto-loads LevelData assets from Resources if path is set.
        /// </summary>
        protected virtual void LoadLevelsFromResources()
        {
            if (string.IsNullOrEmpty(LevelsResourcePath)) return;

            LevelData[] loaded = Resources.LoadAll<LevelData>(LevelsResourcePath);
            if (loaded.Length > 0)
            {
                Levels = loaded;
                Debug.Log($"[LevelSelectScreen] Auto-loaded {loaded.Length} levels from Resources/{LevelsResourcePath}");
            }
        }

        /// <summary>
        /// Fills each card slot with data from the corresponding Levels entry.
        /// Extra cards are hidden; missing data is skipped.
        /// </summary>
        protected virtual void PopulateCards()
        {
            if (CardSlots == null) return;

            for (int i = 0; i < CardSlots.Length; i++)
            {
                if (CardSlots[i] == null) continue;

                if (Levels != null && i < Levels.Length && Levels[i] != null)
                {
                    CardSlots[i].gameObject.SetActive(true);
                    CardSlots[i].Populate(Levels[i]);
                }
                else
                {
                    CardSlots[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Smooth fade-in entrance using MMFade (unscaled time).
        /// </summary>
        protected virtual void FadeIn()
        {
            if (_contentCanvasGroup == null) return;

            _contentCanvasGroup.alpha = 0f;
            StartCoroutine(MMFade.FadeCanvasGroup(_contentCanvasGroup, _fadeInDuration, 1f, true));
        }

        /// <summary>
        /// Called by a "Back" button to return to the start screen.
        /// </summary>
        public virtual void GoBack()
        {
            MMSceneLoadingManager.LoadScene("StartScreen");
        }
    }
}
