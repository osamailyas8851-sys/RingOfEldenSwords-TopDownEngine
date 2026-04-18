using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Magnetic snap carousel — one card visible at a time.
    /// At Start(), auto-sizes every card to match the viewport width so exactly
    /// one card fills the screen regardless of device resolution.
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Snap Scroll Rect")]
    [RequireComponent(typeof(ScrollRect))]
    public class SnapScrollRect : TopDownMonoBehaviour,
        IBeginDragHandler, IEndDragHandler
    {
        [Header("Snap Settings")]
        [Tooltip("How fast the magnetic pull snaps to the target card.")]
        [SerializeField] protected float SnapSpeed = 12f;

        [Tooltip("Minimum swipe delta (normalized 0-1) to advance to the next card.")]
        [SerializeField] protected float SwipeThreshold = 0.05f;

        [Tooltip("Number of child cards (auto-detected if 0).")]
        [SerializeField] protected int CardCount = 0;

        /// <summary>Current snapped card index (0-based).</summary>
        public int CurrentIndex { get; protected set; }

        protected ScrollRect _scrollRect;
        protected bool _isDragging;
        protected bool _isSnapping;
        protected float _snapTarget;
        protected float _dragStartPos;

        protected virtual void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        protected virtual void Start()
        {
            if (CardCount <= 0 && _scrollRect.content != null)
                CardCount = _scrollRect.content.childCount;

            // Auto-size cards to fill exactly one viewport width each
            AutoSizeCards();

            CurrentIndex = 0;
            if (CardCount > 1)
                _scrollRect.horizontalNormalizedPosition = 0f;
        }

        /// <summary>
        /// Resizes every card to match the viewport width and adjusts
        /// the content container so each card = one full page.
        /// </summary>
        protected virtual void AutoSizeCards()
        {
            if (_scrollRect.content == null || CardCount == 0) return;

            // Determine viewport width
            RectTransform viewportRT = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : _scrollRect.GetComponent<RectTransform>();
            float vpWidth = viewportRT.rect.width;
            if (vpWidth <= 0) return;

            // Disable layout group spacing/padding so we have full control
            HorizontalLayoutGroup hlg = _scrollRect.content.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 0;
                hlg.padding = new RectOffset(0, 0, hlg.padding.top, hlg.padding.bottom);
            }

            // Resize each card to viewport width
            foreach (Transform child in _scrollRect.content)
            {
                RectTransform cardRT = child.GetComponent<RectTransform>();
                if (cardRT != null)
                    cardRT.sizeDelta = new Vector2(vpWidth, cardRT.sizeDelta.y);

                LayoutElement le = child.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = vpWidth;
                    le.preferredWidth = vpWidth;
                }
            }

            // Set content total width = all cards side by side
            RectTransform contentRT = _scrollRect.content;
            contentRT.sizeDelta = new Vector2(vpWidth * CardCount, contentRT.sizeDelta.y);
        }

        protected virtual void Update()
        {
            if (_isDragging || !_isSnapping) return;
            if (CardCount <= 1) return;

            // Kill ScrollRect inertia so our snap takes over
            _scrollRect.velocity = Vector2.zero;

            float current = _scrollRect.horizontalNormalizedPosition;
            _scrollRect.horizontalNormalizedPosition =
                Mathf.Lerp(current, _snapTarget, Time.unscaledDeltaTime * SnapSpeed);

            if (Mathf.Abs(current - _snapTarget) < 0.0005f)
            {
                _scrollRect.horizontalNormalizedPosition = _snapTarget;
                _isSnapping = false;
            }
        }

        public virtual void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _isSnapping = false;
            _dragStartPos = _scrollRect.horizontalNormalizedPosition;
        }

        public virtual void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            SnapWithVelocity();
        }

        /// <summary>
        /// Velocity-aware snap: if the user swiped far enough, advance one card
        /// in the swipe direction. Otherwise snap back to the current card.
        /// </summary>
        protected virtual void SnapWithVelocity()
        {
            if (CardCount <= 1) return;

            float stepSize = 1f / (CardCount - 1);
            float currentPos = _scrollRect.horizontalNormalizedPosition;
            float delta = currentPos - _dragStartPos;

            int targetIndex = CurrentIndex;

            if (Mathf.Abs(delta) > SwipeThreshold)
            {
                // Swiped far/fast enough — advance in swipe direction
                targetIndex += (delta > 0) ? 1 : -1;
            }
            else
            {
                // Small drag — snap to nearest
                targetIndex = Mathf.RoundToInt(currentPos / stepSize);
            }

            CurrentIndex = Mathf.Clamp(targetIndex, 0, CardCount - 1);
            _snapTarget = CurrentIndex * stepSize;
            _isSnapping = true;
        }

        /// <summary>Programmatically snap to a specific card index.</summary>
        public virtual void SnapToIndex(int index)
        {
            if (CardCount <= 1) return;
            CurrentIndex = Mathf.Clamp(index, 0, CardCount - 1);
            _snapTarget = CurrentIndex * (1f / (CardCount - 1));
            _isSnapping = true;
        }

        /// <summary>Move to next card.</summary>
        public virtual void MoveRight() { SnapToIndex(CurrentIndex + 1); }

        /// <summary>Move to previous card.</summary>
        public virtual void MoveLeft() { SnapToIndex(CurrentIndex - 1); }
    }
}
