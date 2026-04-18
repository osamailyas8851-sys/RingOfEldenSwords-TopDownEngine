using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MoreMountains.TopDownEngine
{
    // ── Tab Button ──────────────────────────────────────────────────────

    /// <summary>
    /// A single tab in a <see cref="TabBar"/>. Clicking it notifies the bar
    /// to show the matching content panel (by index) and updates its visual
    /// state (selected / deselected colors).
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Tab Button")]
    [RequireComponent(typeof(Button))]
    public class TabButton : MonoBehaviour
    {
        [Tooltip("Zero-based index matching the content panel this tab shows.")]
        public int Index;

        [Header("Visuals")]
        [Tooltip("Background image that switches color on selection.")]
        public Image Background;
        [Tooltip("Label that switches color on selection.")]
        public TMP_Text Label;

        [Header("Colors")]
        public Color SelectedBackground   = new Color(0.22f, 0.22f, 0.30f, 1f);
        public Color DeselectedBackground = new Color(0.10f, 0.10f, 0.14f, 1f);
        public Color SelectedLabel        = Color.white;
        public Color DeselectedLabel      = new Color(0.6f, 0.6f, 0.65f, 1f);

        protected TabBar _bar;
        protected Button _button;

        protected virtual void Awake()
        {
            _bar    = GetComponentInParent<TabBar>();
            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(OnClick);
        }

        protected virtual void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(OnClick);
        }

        protected virtual void OnClick()
        {
            if (_bar != null) _bar.Select(Index);
        }

        public virtual void SetSelected(bool selected)
        {
            if (Background != null) Background.color = selected ? SelectedBackground : DeselectedBackground;
            if (Label != null)      Label.color      = selected ? SelectedLabel      : DeselectedLabel;
        }
    }

    // ── Tab Bar ─────────────────────────────────────────────────────────

    /// <summary>
    /// Drives a simple tab navigation: one <see cref="TabButton"/> per tab,
    /// one content GameObject per tab. Exactly one content panel is active
    /// at a time, and the matching tab is highlighted.
    /// Lightweight, engine-agnostic replacement for MMDebugMenuTabManager
    /// (lives outside TDE core so we don't modify third-party code).
    /// </summary>
    [AddComponentMenu("TopDown Engine/GUI/Tab Bar")]
    public class TabBar : MonoBehaviour
    {
        [Tooltip("Tab buttons in display order. Their Index fields should be 0..N-1.")]
        public List<TabButton> Tabs = new List<TabButton>();

        [Tooltip("Content GameObjects in the same order as Tabs.")]
        public List<GameObject> Contents = new List<GameObject>();

        [Tooltip("Index of the tab to activate on Start.")]
        public int DefaultTab = 0;

        protected virtual void Start()
        {
            Select(DefaultTab);
        }

        /// <summary>
        /// Activates the tab (and its content panel) at the given index.
        /// </summary>
        public virtual void Select(int index)
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                if (Tabs[i] != null)
                    Tabs[i].SetSelected(i == index);
            }
            for (int i = 0; i < Contents.Count; i++)
            {
                if (Contents[i] != null)
                    Contents[i].SetActive(i == index);
            }
        }
    }
}
