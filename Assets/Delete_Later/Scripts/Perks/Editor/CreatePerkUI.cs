using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MoreMountains.TopDownEngine
{
    public static class CreatePerkUI
    {
        // Card dimensions
        private const float CardWidth     = 460f;
        private const float CardHeight    = 110f;
        private const float CardSpacing   = 14f;
        private const float IconSize      = 80f;

        // Panel sizing
        private const float PanelWidth    = 540f;
        private const float PanelHeight   = 660f;

        [MenuItem("Tools/Perk System/Create Perk Selection UI")]
        public static void Create()
        {
            // Target Canvas (sibling of HUD, PauseSplash, etc.)
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("[CreatePerkUI] Could not find 'Canvas' GameObject in scene.");
                return;
            }

            // Also clean up any old PerkSelectionPanel that may have been put under HUD
            GameObject hud = GameObject.Find("HUD");
            if (hud != null)
            {
                Transform oldPanel = hud.transform.Find("PerkSelectionPanel");
                if (oldPanel != null)
                    Undo.DestroyObjectImmediate(oldPanel.gameObject);
            }

            // Remove existing PerkSplash under Canvas
            Transform existing = canvas.transform.Find("PerkSplash");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            // ── Root (full-screen, invisible — holds PerkSelectionUI) ────────
            // No Image on root: the dark backdrop is handled by the scene MMFader.
            // Root is just an activation container.
            GameObject root = CreateUIObject("PerkSplash", canvas.transform);
            StretchFull(root.GetComponent<RectTransform>());
            var perkSelUI = root.AddComponent<PerkSelectionUI>(); // wired below

            // ── Popup card (with CanvasGroup for fade) ──────────────────────
            GameObject popup = CreateUIObject("Popup", root.transform);
            var popupRect = popup.GetComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            popupRect.anchoredPosition = Vector2.zero;

            var popupImg = popup.AddComponent<Image>();
            popupImg.color = new Color(0.08f, 0.10f, 0.14f, 1f);

            // CanvasGroup for MMFade.FadeCanvasGroup()
            var popupCG = popup.AddComponent<CanvasGroup>();
            popupCG.alpha          = 0f;  // starts invisible
            popupCG.interactable   = false;
            popupCG.blocksRaycasts = false;

            // ── Banner ──────────────────────────────────────────────────────
            GameObject banner = CreateUIObject("Banner", popup.transform);
            var bannerRect = banner.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0f, 1f);
            bannerRect.anchorMax = new Vector2(1f, 1f);
            bannerRect.sizeDelta = new Vector2(0f, 90f);
            bannerRect.anchoredPosition = new Vector2(0f, -45f);
            var bannerImg = banner.AddComponent<Image>();
            bannerImg.color = new Color(0.72f, 0.57f, 0.22f, 1f); // gold

            GameObject levelUpText = CreateUIObject("LevelUpText", banner.transform);
            StretchFull(levelUpText.GetComponent<RectTransform>());
            var levelUpTMP = levelUpText.AddComponent<TextMeshProUGUI>();
            levelUpTMP.text = "Level Up!";
            levelUpTMP.fontSize = 38f;
            levelUpTMP.fontStyle = FontStyles.Bold;
            levelUpTMP.alignment = TextAlignmentOptions.Center;
            levelUpTMP.color = new Color(0.10f, 0.06f, 0.01f, 1f);

            // ── "Select a Skill" subtitle ───────────────────────────────────
            GameObject subtitle = CreateUIObject("SubtitleText", popup.transform);
            var subRect = subtitle.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.sizeDelta = new Vector2(0f, 36f);
            subRect.anchoredPosition = new Vector2(0f, -108f);
            var subTMP = subtitle.AddComponent<TextMeshProUGUI>();
            subTMP.text = "Select a Skill";
            subTMP.fontSize = 20f;
            subTMP.fontStyle = FontStyles.Bold;
            subTMP.alignment = TextAlignmentOptions.Center;
            subTMP.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            // ── Cards container (VerticalLayoutGroup) ───────────────────────
            // Sits below the subtitle; height is driven by its children (ContentSizeFitter).
            const float BannerH = 90f;
            const float SubH    = 36f;
            const float GapTop  = 20f;

            GameObject cardsContainer = CreateUIObject("CardsContainer", popup.transform);
            var containerRect = cardsContainer.GetComponent<RectTransform>();
            // Anchor: full width, top-aligned; top edge starts after banner + subtitle + gap
            containerRect.anchorMin        = new Vector2(0f, 1f);
            containerRect.anchorMax        = new Vector2(1f, 1f);
            containerRect.offsetMin        = new Vector2(20f, 0f);   // horizontal padding
            containerRect.offsetMax        = new Vector2(-20f, 0f);
            containerRect.anchoredPosition = new Vector2(0f, -(BannerH + SubH + GapTop));
            containerRect.sizeDelta        = new Vector2(containerRect.sizeDelta.x, 0f); // height driven by children

            var vlg = cardsContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing            = CardSpacing;
            vlg.childAlignment     = TextAnchor.UpperCenter;
            vlg.childControlWidth  = true;   // stretch cards to container width
            vlg.childControlHeight = false;  // cards keep their own fixed height
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.padding            = new RectOffset(0, 0, 0, 0);

            // ContentSizeFitter: container height auto-fits to its children
            var csf = cardsContainer.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            PerkCardUI[] cards = new PerkCardUI[3];

            for (int i = 0; i < 3; i++)
            {
                // Card background — parented to CardsContainer, no manual position needed
                GameObject card = CreateUIObject("PerkCard" + (i + 1), cardsContainer.transform);
                var cardRect = card.GetComponent<RectTransform>();
                cardRect.sizeDelta = new Vector2(0f, CardHeight); // width controlled by VLG

                // LayoutElement so VLG knows the preferred height
                var le = card.AddComponent<LayoutElement>();
                le.preferredHeight = CardHeight;
                le.flexibleWidth   = 1f;

                var cardImg = card.AddComponent<Image>();
                cardImg.color = new Color(0.22f, 0.34f, 0.52f, 1f);

                var button = card.AddComponent<Button>();
                var colors = button.colors;
                colors.normalColor      = Color.white;
                colors.highlightedColor = new Color(1.0f, 1.0f, 0.75f, 1f);
                colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
                button.colors = colors;

                var cardUI = card.AddComponent<PerkCardUI>();

                // Icon frame
                GameObject iconFrame = CreateUIObject("IconFrame", card.transform);
                var iconFrameRect = iconFrame.GetComponent<RectTransform>();
                iconFrameRect.anchorMin = new Vector2(0f, 0.5f);
                iconFrameRect.anchorMax = new Vector2(0f, 0.5f);
                iconFrameRect.sizeDelta = new Vector2(IconSize + 10f, IconSize + 10f);
                iconFrameRect.anchoredPosition = new Vector2(IconSize * 0.5f + 15f, 0f);
                var iconFrameImg = iconFrame.AddComponent<Image>();
                iconFrameImg.color = new Color(0.08f, 0.10f, 0.14f, 1f);

                // Icon image
                GameObject iconGO = CreateUIObject("Icon", iconFrame.transform);
                StretchFull(iconGO.GetComponent<RectTransform>());
                var iconRT = iconGO.GetComponent<RectTransform>();
                iconRT.offsetMin = new Vector2(6f, 6f);
                iconRT.offsetMax = new Vector2(-6f, -6f);
                var iconImage = iconGO.AddComponent<Image>();
                iconImage.color = Color.white;

                // Perk name
                GameObject nameGO = CreateUIObject("PerkName", card.transform);
                var nameRect = nameGO.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0f, 0f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.offsetMin = new Vector2(IconSize + 10f + 30f + 10f, 0f);
                nameRect.offsetMax = new Vector2(-16f, 0f);
                var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
                nameTMP.text = "Perk Name";
                nameTMP.fontSize = 22f;
                nameTMP.fontStyle = FontStyles.Bold;
                nameTMP.alignment = TextAlignmentOptions.Left;
                nameTMP.color = Color.white;

                // Wire PerkCardUI
                var so = new SerializedObject(cardUI);
                so.FindProperty("_iconImage").objectReferenceValue       = iconImage;
                so.FindProperty("_nameText").objectReferenceValue        = nameTMP;
                so.FindProperty("_descriptionText").objectReferenceValue = null;
                so.ApplyModifiedProperties();

                // Wire button click
                var onClick = new SerializedObject(button);
                var calls   = onClick.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                calls.arraySize = 1;
                var call = calls.GetArrayElementAtIndex(0);
                call.FindPropertyRelative("m_Target").objectReferenceValue = cardUI;
                call.FindPropertyRelative("m_MethodName").stringValue      = "OnCardClicked";
                call.FindPropertyRelative("m_Mode").intValue               = 1;
                call.FindPropertyRelative("m_CallState").intValue          = 2;
                onClick.ApplyModifiedProperties();

                cards[i] = cardUI;
            }

            // ── Wire PerkSelectionUI fields ─────────────────────────────────
            var perkUISO = new SerializedObject(perkSelUI);
            // _cards
            var cardsProp = perkUISO.FindProperty("_cards");
            cardsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            // _popupCanvasGroup
            perkUISO.FindProperty("_popupCanvasGroup").objectReferenceValue = popupCG;
            perkUISO.ApplyModifiedProperties();

            // Start hidden
            root.SetActive(false);

            Undo.RegisterCreatedObjectUndo(root, "Create Perk Selection UI");
            EditorUtility.SetDirty(canvas);

            Debug.Log("[CreatePerkUI] PerkSplash created under Canvas (with CanvasGroup fade + MMFader backdrop).");
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
