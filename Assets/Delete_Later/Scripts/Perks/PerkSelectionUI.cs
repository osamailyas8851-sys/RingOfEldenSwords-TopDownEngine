using System.Collections;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Perk selection overlay that uses TDE's CanvasGroup + MMFade pattern
    /// (same approach as ButtonPrompt / DialogueBox) for smooth fade in/out,
    /// and MMFadeEvent for the scene-wide fader backdrop.
    /// Works in unscaled time so it animates while the game is paused.
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Perk Selection UI")]
    public class PerkSelectionUI : TopDownMonoBehaviour
    {
        [Header("Card Slots")]
        [SerializeField] protected PerkCardUI[] _cards;

        [Header("Fade Settings")]
        [Tooltip("CanvasGroup on the popup content panel (Popup child).")]
        [SerializeField] protected CanvasGroup _popupCanvasGroup;

        [Tooltip("Duration of the fade-in animation (unscaled time).")]
        [SerializeField] protected float _fadeInDuration = 0.3f;

        [Tooltip("Duration of the fade-out animation (unscaled time).")]
        [SerializeField] protected float _fadeOutDuration = 0.2f;

        [Header("Backdrop Fader (MMFader)")]
        [Tooltip("Trigger the scene MMFader for a dark backdrop. Set to -1 to disable.")]
        [SerializeField] protected int _faderID = 0;

        [Tooltip("Target backdrop opacity (0-1).")]
        [SerializeField] protected float _backdropOpacity = 0.7f;

        protected Coroutine _fadeCoroutine;

        // ── Public API ──────────────────────────────────────────────────

        public virtual void Show(PerkDefinition[] perks, PerkManager manager)
        {
            // Activate root so CanvasGroup is visible
            gameObject.SetActive(true);

            // Setup cards
            for (int i = 0; i < _cards.Length; i++)
            {
                if (i < perks.Length)
                {
                    _cards[i].gameObject.SetActive(true);
                    _cards[i].Setup(perks[i], manager);
                }
                else
                {
                    _cards[i].gameObject.SetActive(false);
                }
            }

            // Fade in popup
            StopFade();
            if (_popupCanvasGroup != null)
            {
                _popupCanvasGroup.alpha          = 0f;
                _popupCanvasGroup.interactable   = false;
                _fadeCoroutine = StartCoroutine(FadeInCo());
            }

            // Trigger scene-wide MMFader backdrop
            if (_faderID >= 0)
            {
                MMFadeEvent.Trigger(_fadeInDuration, _backdropOpacity,
                    new MMTweenType(MMTween.MMTweenCurve.EaseInCubic),
                    _faderID, true);
            }
        }

        public virtual void Hide()
        {
            StopFade();

            if (_popupCanvasGroup != null && gameObject.activeInHierarchy)
            {
                _popupCanvasGroup.interactable = false;
                _fadeCoroutine = StartCoroutine(FadeOutCo());
            }
            else
            {
                // Fallback: instant hide
                gameObject.SetActive(false);
            }

            // Clear scene-wide MMFader backdrop
            if (_faderID >= 0)
            {
                MMFadeEvent.Trigger(_fadeOutDuration, 0f,
                    new MMTweenType(MMTween.MMTweenCurve.EaseOutCubic),
                    _faderID, true);
            }
        }

        // ── Fade Coroutines (unscaled time — works while paused) ────────

        protected virtual IEnumerator FadeInCo()
        {
            yield return MMFade.FadeCanvasGroup(
                _popupCanvasGroup, _fadeInDuration, 1f, true);

            _popupCanvasGroup.interactable   = true;
            _popupCanvasGroup.blocksRaycasts = true;
            _fadeCoroutine = null;
        }

        protected virtual IEnumerator FadeOutCo()
        {
            yield return MMFade.FadeCanvasGroup(
                _popupCanvasGroup, _fadeOutDuration, 0f, true);

            _popupCanvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            _fadeCoroutine = null;
        }

        protected virtual void StopFade()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }
    }
}
