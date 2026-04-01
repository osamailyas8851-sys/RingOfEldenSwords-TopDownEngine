using UnityEngine;
using TMPro;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Attach to the XPBar GameObject in the HUD.
    /// Listens for XPChangeEvents broadcast by XP.cs and updates the progress bar and level text.
    /// References are auto-found at runtime if not assigned in the Inspector.
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/XP Bar UI")]
    public class XPBarUI : MonoBehaviour, MMEventListener<XPChangeEvent>
    {
        [Header("UI Bindings")]
        [Tooltip("The MMProgressBar to fill. Auto-found on this GameObject if left empty.")]
        public MMProgressBar TargetProgressBar;

        [Tooltip("TMP text for level display. Auto-found by name 'LevelText' in scene if left empty.")]
        public TMP_Text LevelText;

        [Header("Formatting")]
        [Tooltip("Prefix shown before the level number (e.g. 'Lv. ' shows 'Lv. 1').")]
        public string LevelPrefix = "Lv. ";

        protected virtual void Awake()
        {
            if (TargetProgressBar == null)
                TargetProgressBar = GetComponent<MMProgressBar>();

            if (LevelText == null)
            {
                GameObject go = GameObject.Find("LevelText");
                if (go != null)
                    LevelText = go.GetComponent<TMP_Text>();
            }

            if (TargetProgressBar == null)
                Debug.LogWarning("[XPBarUI] No MMProgressBar found on this GameObject. XP bar will not update.", this);
            if (LevelText == null)
                Debug.LogWarning("[XPBarUI] No 'LevelText' TMP_Text found in scene. Level text will not update.", this);
        }

        protected virtual void Start()
        {
            // Reset bar to empty — done in Start so MMProgressBar.Initialization() has already run in its Awake
            // The correct initial values will arrive via XPChangeEvent fired by XP.cs's Start()
            if (TargetProgressBar != null)
                TargetProgressBar.UpdateBar(0f, 0f, 1f);
        }

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<XPChangeEvent>();
        }

        protected virtual void OnDisable()
        {
            this.MMEventStopListening<XPChangeEvent>();
        }

        /// <summary>
        /// Receives XPChangeEvents from the player's XP.cs and updates the HUD.
        /// </summary>
        public virtual void OnMMEvent(XPChangeEvent xpEvent)
        {
            if (TargetProgressBar != null)
                TargetProgressBar.UpdateBar(xpEvent.CurrentXP, 0f, xpEvent.MaxXP);

            if (LevelText != null)
                LevelText.text = LevelPrefix + xpEvent.CurrentLevel;
        }
    }
}
