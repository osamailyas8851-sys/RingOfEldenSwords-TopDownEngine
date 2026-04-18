using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using MoreMountains.Tools;

namespace MoreMountains.TopDownEngine
{
    /// <summary>
    /// Editor tool that builds a LevelSelect.unity scene with a draggable
    /// horizontal snap-carousel.
    /// Tools > Level Select > Create Level Select Scene
    /// </summary>
    public static class CreateLevelSelectUI
    {
        // ── Design constants ────────────────────────────────────────────
        const float REF_W        = 1080f;
        const float REF_H        = 1920f;
        const float CARD_W       = 200f;
        const float CARD_H       = 360f;
        const float CARD_SPACING = 20f;
        const float BANNER_H     = 200f;
        const float TAB_BAR_H    = 160f;
        const float ARROW_SIZE   = 60f;

        static readonly string[] TAB_NAMES = { "Inventory", "Growth", "Base", "Campsite", "Shop" };
        const int BASE_TAB_INDEX = 2;

        static TMP_FontAsset _font;

        [MenuItem("Tools/Level Select/Create Level Select Scene")]
        public static void CreateScene()
        {
            // ── Resolve TMP font ────────────────────────────────────────
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (_font == null)
                Debug.LogWarning("[CreateLevelSelectUI] Could not find LiberationSans SDF. Text may be invisible.");

            // ── New scene ───────────────────────────────────────────────
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Portrait Lock (runs before anything else via execution order) ──
            var lockGO = new GameObject("PortraitLock");
            lockGO.AddComponent<PortraitLock>();

            // ── Camera ──────────────────────────────────────────────────
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.1f);
            cam.orthographic = true;
            camGO.tag = "MainCamera";

            // ── EventSystem ─────────────────────────────────────────────
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();

            // ── Canvas ──────────────────────────────────────────────────
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(REF_W, REF_H);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Content panel (fade target) ─────────────────────────────
            var contentGO = CreateChild(canvasGO, "Content");
            StretchFill(contentGO);
            var contentCG = contentGO.AddComponent<CanvasGroup>();
            contentCG.alpha = 1f;

            // ── Background image (full-screen dark) ─────────────────────
            var bgGO = CreateChild(contentGO, "Background");
            StretchFill(bgGO);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.06f, 0.1f);

            // ── Title Banner ────────────────────────────────────────────
            var bannerGO = CreateChild(contentGO, "TitleBanner");
            var bannerRT = bannerGO.GetComponent<RectTransform>();
            bannerRT.anchorMin = new Vector2(0, 1);
            bannerRT.anchorMax = new Vector2(1, 1);
            bannerRT.pivot = new Vector2(0.5f, 1);
            bannerRT.sizeDelta = new Vector2(0, BANNER_H);
            bannerRT.anchoredPosition = Vector2.zero;

            // ── Player Level Display (top-left of banner) ───────────────
            var levelGO = CreateChild(bannerGO, "PlayerLevelDisplay");
            var levelRT = levelGO.GetComponent<RectTransform>();
            levelRT.anchorMin = new Vector2(0, 0);
            levelRT.anchorMax = new Vector2(0.5f, 1);
            levelRT.offsetMin = new Vector2(24, 10);
            levelRT.offsetMax = new Vector2(0, -10);

            var levelVLG = levelGO.AddComponent<VerticalLayoutGroup>();
            levelVLG.spacing = 8;
            levelVLG.childAlignment = TextAnchor.MiddleLeft;
            levelVLG.childControlWidth = true;
            levelVLG.childControlHeight = false;
            levelVLG.childForceExpandWidth = true;
            levelVLG.childForceExpandHeight = false;
            levelVLG.padding = new RectOffset(0, 0, 10, 10);

            var levelRowGO = CreateChild(levelGO, "LevelRow");
            levelRowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 52);
            var levelRowLE = levelRowGO.AddComponent<LayoutElement>();
            levelRowLE.minHeight = 52; levelRowLE.preferredHeight = 52;
            var levelRowImg = levelRowGO.AddComponent<Image>();
            levelRowImg.color = new Color(0f, 0f, 0f, 0.3f);
            var levelHLG = levelRowGO.AddComponent<HorizontalLayoutGroup>();
            levelHLG.spacing = 10;
            levelHLG.childAlignment = TextAnchor.MiddleLeft;
            levelHLG.childControlWidth = false;
            levelHLG.childControlHeight = true;
            levelHLG.childForceExpandWidth = false;
            levelHLG.childForceExpandHeight = true;
            levelHLG.padding = new RectOffset(16, 10, 0, 0);

            var levelLabelTMP = CreateTMPText(levelRowGO, "LevelLabel", "LVL", 26,
                FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.7f, 0.9f, 1f));
            levelLabelTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 0);
            var levelValueTMP = CreateTMPText(levelRowGO, "LevelValue", "1", 36,
                FontStyles.Bold, TextAlignmentOptions.Left, Color.white);
            levelValueTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 0);

            // ── Currency Display (top-right of banner) ──────────────────
            var currencyGO = CreateChild(bannerGO, "CurrencyDisplay");
            var currencyRT = currencyGO.GetComponent<RectTransform>();
            currencyRT.anchorMin = new Vector2(0.5f, 0);
            currencyRT.anchorMax = new Vector2(1f, 1f);
            currencyRT.offsetMin = new Vector2(0, 10);
            currencyRT.offsetMax = new Vector2(-24, -10);

            var currencyVLG = currencyGO.AddComponent<VerticalLayoutGroup>();
            currencyVLG.spacing = 8;
            currencyVLG.childAlignment = TextAnchor.MiddleRight;
            currencyVLG.childControlWidth = true;
            currencyVLG.childControlHeight = false;
            currencyVLG.childForceExpandWidth = true;
            currencyVLG.childForceExpandHeight = false;
            currencyVLG.padding = new RectOffset(0, 0, 10, 10);

            // Coin row
            var coinRowGO = CreateChild(currencyGO, "CoinRow");
            coinRowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 52);
            var coinRowLE = coinRowGO.AddComponent<LayoutElement>();
            coinRowLE.minHeight = 52; coinRowLE.preferredHeight = 52;
            var coinRowImg = coinRowGO.AddComponent<Image>();
            coinRowImg.color = new Color(0f, 0f, 0f, 0.3f);
            var coinHLG = coinRowGO.AddComponent<HorizontalLayoutGroup>();
            coinHLG.spacing = 10;
            coinHLG.childAlignment = TextAnchor.MiddleRight;
            coinHLG.childControlWidth = false;
            coinHLG.childControlHeight = true;
            coinHLG.childForceExpandWidth = false;
            coinHLG.childForceExpandHeight = true;
            coinHLG.padding = new RectOffset(10, 16, 0, 0);

            var coinLabelTMP = CreateTMPText(coinRowGO, "CoinLabel", "COIN", 26,
                FontStyles.Normal, TextAlignmentOptions.Right, new Color(1f, 0.85f, 0.3f));
            coinLabelTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 0);
            var coinCountTMP = CreateTMPText(coinRowGO, "CoinCount", "0", 36,
                FontStyles.Bold, TextAlignmentOptions.Right, Color.white);
            coinCountTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 0);

            // Diamond row
            var diamondRowGO = CreateChild(currencyGO, "DiamondRow");
            diamondRowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 52);
            var diamondRowLE = diamondRowGO.AddComponent<LayoutElement>();
            diamondRowLE.minHeight = 52; diamondRowLE.preferredHeight = 52;
            var diamondRowImg = diamondRowGO.AddComponent<Image>();
            diamondRowImg.color = new Color(0f, 0f, 0f, 0.3f);
            var diamondHLG = diamondRowGO.AddComponent<HorizontalLayoutGroup>();
            diamondHLG.spacing = 10;
            diamondHLG.childAlignment = TextAnchor.MiddleRight;
            diamondHLG.childControlWidth = false;
            diamondHLG.childControlHeight = true;
            diamondHLG.childForceExpandWidth = false;
            diamondHLG.childForceExpandHeight = true;
            diamondHLG.padding = new RectOffset(10, 16, 0, 0);

            var diamondLabelTMP = CreateTMPText(diamondRowGO, "DiamondLabel", "GEM", 26,
                FontStyles.Normal, TextAlignmentOptions.Right, new Color(0.4f, 0.8f, 1f));
            diamondLabelTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(80, 0);
            var diamondCountTMP = CreateTMPText(diamondRowGO, "DiamondCount", "0", 36,
                FontStyles.Bold, TextAlignmentOptions.Right, Color.white);
            diamondCountTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 0);

            // Wire CurrencyDisplay component
            var currencyDisplay = currencyGO.AddComponent<CurrencyDisplay>();
            var currSO = new SerializedObject(currencyDisplay);
            currSO.FindProperty("_coinsText").objectReferenceValue       = coinCountTMP;
            currSO.FindProperty("_diamondsText").objectReferenceValue    = diamondCountTMP;
            currSO.FindProperty("_playerLevelText").objectReferenceValue = levelValueTMP;
            currSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Tab Content Root (between banner and tab bar) ───────────
            var tabRootGO = CreateChild(contentGO, "TabContentRoot");
            var tabRootRT = tabRootGO.GetComponent<RectTransform>();
            tabRootRT.anchorMin = new Vector2(0, 0);
            tabRootRT.anchorMax = new Vector2(1, 1);
            tabRootRT.offsetMin = new Vector2(0, TAB_BAR_H);
            tabRootRT.offsetMax = new Vector2(0, -BANNER_H);

            // ── Tab Panels (one per tab name) ───────────────────────────
            var tabPanels = new GameObject[TAB_NAMES.Length];
            for (int i = 0; i < TAB_NAMES.Length; i++)
            {
                var panelGO = CreateChild(tabRootGO, $"Tab_{TAB_NAMES[i]}_Panel");
                StretchFill(panelGO);
                // Non-Base tabs get a placeholder label so the area isn't empty
                if (i != BASE_TAB_INDEX)
                {
                    var placeholderTMP = CreateTMPText(panelGO, "Placeholder", TAB_NAMES[i].ToUpper(),
                        72, FontStyles.Bold, TextAlignmentOptions.Center,
                        new Color(0.3f, 0.3f, 0.35f));
                    StretchFill(placeholderTMP.gameObject);
                }
                tabPanels[i] = panelGO;
            }
            var baseTabGO = tabPanels[BASE_TAB_INDEX];

            // ── Scroll Viewport (lives inside Base tab) ─────────────────
            var scrollGO = CreateChild(baseTabGO, "ScrollArea");
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = new Vector2(0, 80);  // leave room for page dots
            scrollRT.offsetMax = new Vector2(0, 0);

            // RectMask2D clips children without needing an Image
            scrollGO.AddComponent<RectMask2D>();

            // ── Scroll Content (holds cards) ────────────────────────────
            var scrollContentGO = CreateChild(scrollGO, "ScrollContent");
            var scrollContentRT = scrollContentGO.GetComponent<RectTransform>();
            // Anchor left-center, total width fits all 3 cards + spacing + side padding
            scrollContentRT.anchorMin = new Vector2(0, 0);
            scrollContentRT.anchorMax = new Vector2(0, 1);
            scrollContentRT.pivot = new Vector2(0, 0.5f);
            float sidePad = (REF_W - (CARD_W * 3 + CARD_SPACING * 2)) * 0.5f;
            float totalW = sidePad * 2 + CARD_W * 3 + CARD_SPACING * 2;
            scrollContentRT.sizeDelta = new Vector2(totalW, 0);
            scrollContentRT.anchoredPosition = Vector2.zero;

            var hlg = scrollContentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = CARD_SPACING;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;      // use LayoutElement.preferredWidth
            hlg.childControlHeight = true;     // use LayoutElement.preferredHeight
            hlg.childForceExpandWidth = false; // do NOT stretch past preferredWidth
            hlg.childForceExpandHeight = false;
            int intSidePad = Mathf.RoundToInt((REF_W - (CARD_W * 3 + CARD_SPACING * 2)) * 0.5f);
            hlg.padding = new RectOffset(intSidePad, intSidePad, 20, 20);

            // ── ScrollRect component (draggable) ────────────────────────
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.content = scrollContentRT;
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = false;          // no coasting — SnapScrollRect handles momentum
            scrollRect.scrollSensitivity = 1f;
            scrollRect.viewport = scrollRT;

            // ── SnapScrollRect (snaps to nearest card after drag) ───────
            var snap = scrollGO.AddComponent<SnapScrollRect>();
            var snapSO = new SerializedObject(snap);
            snapSO.FindProperty("SnapSpeed").floatValue = 10f;
            snapSO.FindProperty("CardCount").intValue = 3;
            snapSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Level Cards ─────────────────────────────────────────────
            string[] names = { "Level 1", "Level 2", "Level 3" };
            Color[] accents = {
                new Color(0.3f, 0.8f, 0.3f),
                new Color(0.85f, 0.7f, 0.2f),
                new Color(0.9f, 0.2f, 0.2f)
            };
            string[] descriptions = {
                "A gentle introduction.\nFewer waves, weaker foes.",
                "Standard challenge.\nBalanced enemies and rewards.",
                "Maximum danger.\nAll sword tiers unlocked."
            };

            var cardSlots = new LevelSelectButton[3];
            for (int i = 0; i < 3; i++)
                cardSlots[i] = CreateLevelCard(scrollContentGO, names[i], accents[i], descriptions[i]);

            // ── Left / Right arrow buttons (inside Base tab) ────────────
            CreateArrowButton(baseTabGO, "LeftArrow", true, snap);
            CreateArrowButton(baseTabGO, "RightArrow", false, snap);

            // ── Page dots indicator (inside Base tab) ───────────────────
            CreatePageDots(baseTabGO, 3);

            // ── Bottom Tab Bar ──────────────────────────────────────────
            var tabButtons = CreateTabBar(contentGO, tabPanels);

            // ── LevelSelectScreen on Content ────────────────────────────
            var screen = contentGO.AddComponent<LevelSelectScreen>();
            var screenSO = new SerializedObject(screen);
            screenSO.FindProperty("CardSlots").arraySize = 3;
            for (int i = 0; i < 3; i++)
                screenSO.FindProperty("CardSlots").GetArrayElementAtIndex(i).objectReferenceValue = cardSlots[i];
            screenSO.FindProperty("_contentCanvasGroup").objectReferenceValue = contentCG;
            screenSO.ApplyModifiedPropertiesWithoutUndo();

            // ── MMFader for scene transitions ───────────────────────────
            var faderCanvasGO = new GameObject("MMFader");
            var faderCanvas = faderCanvasGO.AddComponent<Canvas>();
            faderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            faderCanvas.sortingOrder = 10000;
            faderCanvasGO.AddComponent<CanvasScaler>();
            faderCanvasGO.AddComponent<GraphicRaycaster>();

            var faderImgGO = CreateChild(faderCanvasGO, "FaderImage");
            StretchFill(faderImgGO);
            var faderImg = faderImgGO.AddComponent<Image>();
            faderImg.color = Color.black;
            faderImgGO.AddComponent<CanvasGroup>();
            faderImgGO.AddComponent<MMFader>();

            // ── Save scene ──────────────────────────────────────────────
            string scenePath = "Assets/Delete_Later/Scenes/LevelSelect.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuildSettings(scenePath);

            Debug.Log($"[CreateLevelSelectUI] Carousel scene saved to {scenePath}");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(scenePath));
        }

        // ── Helper: Create Level Card ───────────────────────────────────

        static LevelSelectButton CreateLevelCard(GameObject parent, string label, Color accent, string desc)
        {
            var cardGO = CreateChild(parent, $"Card_{label.Replace(' ', '_')}");
            var cardRT = cardGO.GetComponent<RectTransform>();
            // Pin anchors + pivot so HLG can't stretch this rect
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot     = new Vector2(0.5f, 0.5f);
            cardRT.sizeDelta = new Vector2(CARD_W, CARD_H);

            // Card background (visible contrast against dark bg)
            var cardImg = cardGO.AddComponent<Image>();
            cardImg.color = new Color(0.16f, 0.17f, 0.24f);

            // Subtle outline for card edge visibility
            var outline = cardGO.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
            outline.effectDistance = new Vector2(2, -2);

            // Fixed size via LayoutElement (HLG uses these since childControlWidth/Height=true)
            var le = cardGO.AddComponent<LayoutElement>();
            le.minWidth       = CARD_W;
            le.preferredWidth = CARD_W;
            le.flexibleWidth  = 0;
            le.minHeight       = CARD_H;
            le.preferredHeight = CARD_H;
            le.flexibleHeight  = 0;

            cardGO.AddComponent<Button>();

            // Accent top strip
            var stripGO = CreateChild(cardGO, "AccentStrip");
            var stripRT = stripGO.GetComponent<RectTransform>();
            stripRT.anchorMin = new Vector2(0, 1);
            stripRT.anchorMax = new Vector2(1, 1);
            stripRT.pivot = new Vector2(0.5f, 1);
            stripRT.sizeDelta = new Vector2(0, 6);
            stripRT.anchoredPosition = Vector2.zero;
            var stripImg = stripGO.AddComponent<Image>();
            stripImg.color = accent;

            // Level name (upper area)
            var nameTMP = CreateTMPText(cardGO, "NameText", label, 26,
                FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            var nameRT = nameTMP.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.62f);
            nameRT.anchorMax = new Vector2(1, 0.82f);
            nameRT.offsetMin = new Vector2(8, 0);
            nameRT.offsetMax = new Vector2(-8, 0);

            // Description
            var descTMP = CreateTMPText(cardGO, "DescriptionText", desc, 14,
                FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.65f, 0.65f, 0.7f));
            var descRT = descTMP.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 0.42f);
            descRT.anchorMax = new Vector2(1, 0.62f);
            descRT.offsetMin = new Vector2(8, 0);
            descRT.offsetMax = new Vector2(-8, 0);

            // Divider line
            var dividerGO = CreateChild(cardGO, "Divider");
            var dividerRT = dividerGO.GetComponent<RectTransform>();
            dividerRT.anchorMin = new Vector2(0.05f, 0.38f);
            dividerRT.anchorMax = new Vector2(0.95f, 0.38f);
            dividerRT.sizeDelta = new Vector2(0, 1);
            dividerGO.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f);

            // Waves text (bottom left)
            var wavesTMP = CreateTMPText(cardGO, "WavesText", "5 Waves", 16,
                FontStyles.Normal, TextAlignmentOptions.Left, new Color(0.6f, 0.6f, 0.65f));
            var wavesRT = wavesTMP.GetComponent<RectTransform>();
            wavesRT.anchorMin = new Vector2(0, 0.22f);
            wavesRT.anchorMax = new Vector2(1, 0.34f);
            wavesRT.offsetMin = new Vector2(10, 0);
            wavesRT.offsetMax = new Vector2(-10, 0);

            // Difficulty (accent colored)
            var diffTMP = CreateTMPText(cardGO, "DifficultyText", "Difficulty 1/10", 15,
                FontStyles.Bold, TextAlignmentOptions.Center, accent);
            var diffRT = diffTMP.GetComponent<RectTransform>();
            diffRT.anchorMin = new Vector2(0, 0.10f);
            diffRT.anchorMax = new Vector2(1, 0.22f);
            diffRT.offsetMin = new Vector2(6, 0);
            diffRT.offsetMax = new Vector2(-6, 0);

            // Progression text
            var progTMP = CreateTMPText(cardGO, "ProgressionText", "0/10", 14,
                FontStyles.Normal, TextAlignmentOptions.Center, new Color(0.6f, 0.6f, 0.65f));
            var progRT = progTMP.GetComponent<RectTransform>();
            progRT.anchorMin = new Vector2(0, 0.01f);
            progRT.anchorMax = new Vector2(1, 0.10f);
            progRT.offsetMin = new Vector2(6, 0);
            progRT.offsetMax = new Vector2(-6, 0);

            // Wire LevelSelectButton
            var btn = cardGO.AddComponent<LevelSelectButton>();
            var so = new SerializedObject(btn);
            so.FindProperty("_nameText").objectReferenceValue        = nameTMP;
            so.FindProperty("_descriptionText").objectReferenceValue = descTMP;
            so.FindProperty("_wavesText").objectReferenceValue       = wavesTMP;
            so.FindProperty("_difficultyText").objectReferenceValue  = diffTMP;
            so.FindProperty("_progressionText").objectReferenceValue = progTMP;
            so.FindProperty("_accentStrip").objectReferenceValue     = stripImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            return btn;
        }

        // ── Helper: Create Bottom Tab Bar ───────────────────────────────

        static TabButton[] CreateTabBar(GameObject parent, GameObject[] tabPanels)
        {
            var barGO = CreateChild(parent, "TabBar");
            var barRT = barGO.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0, 0);
            barRT.anchorMax = new Vector2(1, 0);
            barRT.pivot     = new Vector2(0.5f, 0);
            barRT.sizeDelta = new Vector2(0, TAB_BAR_H);
            barRT.anchoredPosition = Vector2.zero;

            var barImg = barGO.AddComponent<Image>();
            barImg.color = new Color(0.04f, 0.04f, 0.07f, 1f);

            var barHLG = barGO.AddComponent<HorizontalLayoutGroup>();
            barHLG.spacing = 0;
            barHLG.childAlignment = TextAnchor.MiddleCenter;
            barHLG.childControlWidth = true;
            barHLG.childControlHeight = true;
            barHLG.childForceExpandWidth = true;
            barHLG.childForceExpandHeight = true;
            barHLG.padding = new RectOffset(0, 0, 0, 0);

            var buttons = new TabButton[TAB_NAMES.Length];
            for (int i = 0; i < TAB_NAMES.Length; i++)
            {
                var tabGO = CreateChild(barGO, $"Tab_{TAB_NAMES[i]}");
                var tabImg = tabGO.AddComponent<Image>();
                tabImg.color = new Color(0.10f, 0.10f, 0.14f, 1f);
                tabGO.AddComponent<Button>();

                var labelTMP = CreateTMPText(tabGO, "Label", TAB_NAMES[i].ToUpper(),
                    28, FontStyles.Bold, TextAlignmentOptions.Center,
                    new Color(0.6f, 0.6f, 0.65f));
                StretchFill(labelTMP.gameObject);

                var tb = tabGO.AddComponent<TabButton>();
                var tbSO = new SerializedObject(tb);
                tbSO.FindProperty("Index").intValue                 = i;
                tbSO.FindProperty("Background").objectReferenceValue = tabImg;
                tbSO.FindProperty("Label").objectReferenceValue      = labelTMP;
                tbSO.ApplyModifiedPropertiesWithoutUndo();
                buttons[i] = tb;
            }

            // Wire the TabBar component
            var bar = barGO.AddComponent<TabBar>();
            var barSO = new SerializedObject(bar);
            var tabsProp = barSO.FindProperty("Tabs");
            tabsProp.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
                tabsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            var contentsProp = barSO.FindProperty("Contents");
            contentsProp.arraySize = tabPanels.Length;
            for (int i = 0; i < tabPanels.Length; i++)
                contentsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabPanels[i];
            barSO.FindProperty("DefaultTab").intValue = BASE_TAB_INDEX;
            barSO.ApplyModifiedPropertiesWithoutUndo();

            return buttons;
        }

        // ── Helper: Create Arrow Button ─────────────────────────────────

        static void CreateArrowButton(GameObject parent, string name, bool isLeft, SnapScrollRect snap)
        {
            var btnGO = CreateChild(parent, name);
            var btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(isLeft ? 0 : 1, 0.5f);
            btnRT.anchorMax = new Vector2(isLeft ? 0 : 1, 0.5f);
            btnRT.pivot = new Vector2(0.5f, 0.5f);
            btnRT.sizeDelta = new Vector2(ARROW_SIZE, ARROW_SIZE * 1.5f);
            btnRT.anchoredPosition = new Vector2(isLeft ? 40 : -40, -40);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);

            var arrowTMP = CreateTMPText(btnGO, "ArrowText", isLeft ? "<" : ">", 48,
                FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
            StretchFill(arrowTMP.gameObject);

            var button = btnGO.AddComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                button.onClick,
                isLeft ? new UnityEngine.Events.UnityAction(snap.MoveLeft)
                       : new UnityEngine.Events.UnityAction(snap.MoveRight));
        }

        // ── Helper: Create Page Dots ────────────────────────────────────

        static void CreatePageDots(GameObject parent, int count)
        {
            var dotsGO = CreateChild(parent, "PageDots");
            var dotsRT = dotsGO.GetComponent<RectTransform>();
            dotsRT.anchorMin = new Vector2(0.5f, 0);
            dotsRT.anchorMax = new Vector2(0.5f, 0);
            dotsRT.pivot = new Vector2(0.5f, 0);
            dotsRT.sizeDelta = new Vector2(count * 40, 30);
            dotsRT.anchoredPosition = new Vector2(0, 50);

            var hlg = dotsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            for (int i = 0; i < count; i++)
            {
                var dotGO = CreateChild(dotsGO, $"Dot_{i}");
                dotGO.GetComponent<RectTransform>().sizeDelta = new Vector2(16, 16);
                dotGO.AddComponent<Image>().color = i == 0 ? Color.white : new Color(1, 1, 1, 0.3f);
            }
        }

        // ── Helper: Create TMP Text with font ──────────────────────────

        static TextMeshProUGUI CreateTMPText(GameObject parent, string name, string text,
            float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var go = CreateChild(parent, name);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        // ── Utility helpers ─────────────────────────────────────────────

        static GameObject CreateChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static void StretchFill(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            foreach (var s in scenes)
                if (s.path == scenePath) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[CreateLevelSelectUI] Added {scenePath} to Build Settings.");
        }
    }
}
