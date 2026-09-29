// Scandal Season — Runtime game layer.
// StorySceneView: portrait phone story player.
// - 9:16 vertical layout, always.
// - Scrollable prose via ScrollRect.
// - Tappable decision buttons (instantiated per option), persisted via GameManager.
// - Chapter/scene progress header.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class StorySceneView : MonoBehaviour
{
    [Header("UI — Header")]
    public Text chapterTitleText;
    public Text sceneHeaderText;
    public Text typeBadgeText;
    public Slider progressSlider; // scene X of 40 within chapter

    [Header("UI — Prose (scrollable)")]
    public ScrollRect proseScrollRect;
    public Text proseText; // full narrative prose body, inside scroll content
    public Text synopsisText;

    [Header("UI — Decisions")]
    public Transform decisionButtonContainer; // vertical layout group
    public Button decisionButtonPrefab; // prefab with a Text child
    public Text bodyText; // ritual / sting / fashion / participants (non-decision info)

    [Header("UI — Navigation")]
    public Button continueButton;
    public Button toBoardButton;
    public Text turnsText;

    [Header("Editorial styling (visual lock v1)")]
    [Tooltip("Background image for estate plates / scene art.")]
    public Image backgroundImage;
    [Tooltip("Approved estate plates Sep 28.")]
    public Sprite[] estatePlates;
    [Tooltip("Gold accent for headers and badges.")]
    public Color goldAccent = new Color(0.83f, 0.69f, 0.35f);
    [Tooltip("Light text for dark backgrounds (story view).")]
    public Color lightTextColor = new Color(0.95f, 0.93f, 0.88f);
    [Tooltip("Dim text for secondary labels.")]
    public Color dimTextColor = new Color(0.7f, 0.68f, 0.62f);

    private GameManager _game;
    private SceneDefinitionSO _scene;
    private readonly List<Button> _spawnedDecisionButtons = new List<Button>();
    private int _selectedDecisionIndex = -1;

    private void Start()
    {
        _game = GameManager.Instance;
        EnsurePortraitUI();
        WireFooterButtons();
    }

    private void WireFooterButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinue);
        }
        if (toBoardButton != null)
        {
            toBoardButton.onClick.RemoveAllListeners();
            toBoardButton.onClick.AddListener(() => _game.SetState(GameState.MergeBoard));
        }
    }

    /// <summary>
    /// Self-healing portrait UI: builds the ScrollRect, decision container,
    /// button template, and progress slider in code when the scene does not
    /// provide them. This keeps Main.unity edits out of the critical path —
    /// the view works whether or not the scene was hand-updated.
    /// </summary>
    private void EnsurePortraitUI()
    {
        RectTransform panel = transform as RectTransform;
        if (panel == null) return;

        // The scene builder puts a VerticalLayoutGroup on every panel. It
        // re-anchors children to a single top-left point and leaves their
        // sizeDelta at (0,0), which collapses every Text and rect on the
        // panel to invisible (the title screen's lone "S" was the same
        // disease). We position everything below with explicit anchors,
        // so the layout group is switched off first — synchronously, since
        // Destroy() would not process until end of frame while we force
        // canvas rebuilds below.
        var panelVlg = panel.GetComponent<VerticalLayoutGroup>();
        if (panelVlg != null) panelVlg.enabled = false;

        Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // --- Create prose text if the scene doesn't provide it ---
        if (proseText == null)
        {
            GameObject proseGO = new GameObject("ProseText", typeof(RectTransform), typeof(Text));
            RectTransform proseRT = proseGO.GetComponent<RectTransform>();
            proseRT.SetParent(panel, false);
            var pt = proseGO.GetComponent<Text>();
            pt.font = builtinFont;
            pt.fontSize = 20;
            pt.color = lightTextColor;
            pt.alignment = TextAnchor.UpperLeft;
            pt.horizontalOverflow = HorizontalWrapMode.Wrap;
            pt.verticalOverflow = VerticalWrapMode.Overflow;
            pt.supportRichText = true;
            proseText = pt;
        }
        // --- Create synopsis text if the scene doesn't provide it ---
        if (synopsisText == null)
        {
            GameObject synGO = new GameObject("SynopsisText", typeof(RectTransform), typeof(Text));
            RectTransform synRT = synGO.GetComponent<RectTransform>();
            synRT.SetParent(panel, false);
            var st = synGO.GetComponent<Text>();
            st.font = builtinFont;
            st.fontSize = 16;
            st.fontStyle = FontStyle.Italic;
            st.color = dimTextColor;
            st.alignment = TextAnchor.UpperLeft;
            st.horizontalOverflow = HorizontalWrapMode.Wrap;
            st.verticalOverflow = VerticalWrapMode.Overflow;
            synopsisText = st;
        }

        // --- Create header texts if the scene doesn't provide them ---
        if (chapterTitleText == null)
        {
            GameObject ctGO = new GameObject("ChapterTitle", typeof(RectTransform), typeof(Text));
            ctGO.GetComponent<RectTransform>().SetParent(panel, false);
            var ct = ctGO.GetComponent<Text>();
            ct.font = builtinFont; ct.fontSize = 22; ct.fontStyle = FontStyle.Bold;
            ct.color = goldAccent; ct.alignment = TextAnchor.MiddleLeft;
            ct.horizontalOverflow = HorizontalWrapMode.Wrap;
            chapterTitleText = ct;
        }
        if (sceneHeaderText == null)
        {
            GameObject shGO = new GameObject("SceneHeader", typeof(RectTransform), typeof(Text));
            shGO.GetComponent<RectTransform>().SetParent(panel, false);
            var sh = shGO.GetComponent<Text>();
            sh.font = builtinFont; sh.fontSize = 16;
            sh.color = lightTextColor; sh.alignment = TextAnchor.MiddleLeft;
            sceneHeaderText = sh;
        }
        if (typeBadgeText == null)
        {
            GameObject tbGO = new GameObject("TypeBadge", typeof(RectTransform), typeof(Text));
            tbGO.GetComponent<RectTransform>().SetParent(panel, false);
            var tb = tbGO.GetComponent<Text>();
            tb.font = builtinFont; tb.fontSize = 16; tb.fontStyle = FontStyle.Italic;
            tb.color = goldAccent; tb.alignment = TextAnchor.MiddleRight;
            typeBadgeText = tb;
        }
        if (bodyText == null)
        {
            GameObject bdGO = new GameObject("BodyText", typeof(RectTransform), typeof(Text));
            bdGO.GetComponent<RectTransform>().SetParent(panel, false);
            var bd = bdGO.GetComponent<Text>();
            bd.font = builtinFont; bd.fontSize = 18;
            bd.color = lightTextColor; bd.alignment = TextAnchor.MiddleLeft;
            bd.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText = bd;
        }

        // --- ScrollRect around the prose text ---
        if (proseScrollRect == null)
        {
            GameObject scrollGO = new GameObject("ProseScroll", typeof(RectTransform), typeof(ScrollRect));
            RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.SetParent(panel, false);
            // Fill most of the panel; header above, body/decisions/footer below.
            scrollRT.anchorMin = new Vector2(0.05f, 0.375f);
            scrollRT.anchorMax = new Vector2(0.95f, 0.815f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            RectTransform viewportRT = viewportGO.GetComponent<RectTransform>();
            viewportRT.SetParent(scrollRT, false);
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            var vpImg = viewportGO.GetComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.35f); // subtle dark backing
            viewportGO.GetComponent<Mask>().showMaskGraphic = true;

            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            RectTransform contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.SetParent(viewportRT, false);
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            // NOTE: no VerticalLayoutGroup / ContentSizeFitter here — the
            // builder's layout groups collapse children to zero size, so the
            // scroll content is measured and stacked by hand in
            // LayoutScrollContent().

            // Move synopsis + prose into the scroll content.
            if (synopsisText != null)
            {
                synopsisText.transform.SetParent(contentRT, false);
                var srt = synopsisText.rectTransform;
                srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 1);
            }
            proseText.transform.SetParent(contentRT, false);
            var prt = proseText.rectTransform;
            prt.anchorMin = new Vector2(0, 1); prt.anchorMax = new Vector2(1, 1); prt.pivot = new Vector2(0.5f, 1);

            var sr = scrollGO.GetComponent<ScrollRect>();
            sr.content = contentRT;
            sr.viewport = viewportRT;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 24f;
            proseScrollRect = sr;
        }

        // --- Decision button container (manual vertical stacking; the
        // builder's VerticalLayoutGroup collapses children, so none here).
        // ShowScene resizes/repositions this explicitly per decision (fixed
        // pixel heights, bottom-anchored); these are just sane defaults.
        if (decisionButtonContainer == null)
        {
            GameObject decGO = new GameObject("DecisionButtons", typeof(RectTransform));
            RectTransform decRT = decGO.GetComponent<RectTransform>();
            decRT.SetParent(panel, false);
            decRT.anchorMin = new Vector2(0.05f, 0f);
            decRT.anchorMax = new Vector2(0.95f, 0f);
            decRT.pivot = new Vector2(0.5f, 0f);
            decRT.anchoredPosition = new Vector2(0f, 100f);
            decRT.sizeDelta = new Vector2(0f, 208f); // 3 x 64px + spacing
            decRT.offsetMin = Vector2.zero;
            decRT.offsetMax = Vector2.zero;
            decisionButtonContainer = decRT;
        }

        // --- Decision button template (built in code) ---
        if (decisionButtonPrefab == null)
        {
            GameObject btnGO = new GameObject("DecisionButtonTemplate", typeof(RectTransform),
                typeof(Image), typeof(Button));
            RectTransform btnRT = btnGO.GetComponent<RectTransform>();
            btnRT.SetParent(panel, false);
            var img = btnGO.GetComponent<Image>();
            img.color = new Color(0.22f, 0.28f, 0.22f, 1f);
            GameObject txtGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
            RectTransform txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.SetParent(btnRT, false);
            txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = new Vector2(16, 8); txtRT.offsetMax = new Vector2(-16, -8);
            var txt = txtGO.GetComponent<Text>();
            txt.alignment = TextAnchor.MiddleLeft;
            txt.fontSize = 20;
            txt.color = lightTextColor;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var btn = btnGO.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = new Color(0.22f, 0.28f, 0.22f, 1f);
            cb.highlightedColor = new Color(0.32f, 0.40f, 0.32f, 1f);
            cb.pressedColor = new Color(0.45f, 0.36f, 0.18f, 1f);
            btn.colors = cb;
            // LayoutElement so the VerticalLayoutGroup sizes it.
            var le = btnGO.AddComponent<LayoutElement>();
            le.minHeight = 64f;
            le.preferredHeight = 72f;
            btnGO.SetActive(false); // template only
            decisionButtonPrefab = btn;
        }
        else
        {
            // Ensure the assigned prefab's label uses a real font.
            var t = decisionButtonPrefab.GetComponentInChildren<Text>();
            if (t != null && t.font == null)
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // --- Progress slider ---
        if (progressSlider == null)
        {
            GameObject sliderGO = new GameObject("SceneProgress", typeof(RectTransform), typeof(Slider));
            RectTransform srt = sliderGO.GetComponent<RectTransform>();
            srt.SetParent(panel, false);
            srt.anchorMin = new Vector2(0.05f, 0.828f);
            srt.anchorMax = new Vector2(0.95f, 0.845f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var slider = sliderGO.GetComponent<Slider>();
            slider.minValue = 0; slider.maxValue = 40; slider.value = 1;
            slider.interactable = false;
            // Minimal visuals: background + fill.
            GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.SetParent(srt, false);
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;
            bgGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
            GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.SetParent(srt, false);
            fillRT.anchorMin = new Vector2(0, 0.25f); fillRT.anchorMax = new Vector2(1, 0.75f);
            fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
            fillGO.GetComponent<Image>().color = goldAccent;
            slider.fillRect = fillRT;
            // No handle needed for a progress display.
            progressSlider = slider;
        }

        // Pin the footer buttons to the bottom of the portrait panel.
        // (Decision container sits at 0.105-0.325, so footer stays below 0.10.)
        if (continueButton != null)
        {
            var crt = continueButton.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.05f, 0.02f);
            crt.anchorMax = new Vector2(0.62f, 0.095f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
        }
        if (toBoardButton != null)
        {
            var brt = toBoardButton.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.65f, 0.02f);
            brt.anchorMax = new Vector2(0.95f, 0.095f);
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        }
        // Header texts to the top.
        if (chapterTitleText != null)
        {
            var hrt = chapterTitleText.rectTransform;
            hrt.anchorMin = new Vector2(0.05f, 0.90f);
            hrt.anchorMax = new Vector2(0.95f, 0.97f);
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
        }
        if (sceneHeaderText != null)
        {
            var hrt = sceneHeaderText.rectTransform;
            hrt.anchorMin = new Vector2(0.05f, 0.875f);
            hrt.anchorMax = new Vector2(0.55f, 0.90f);
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
        }
        if (typeBadgeText != null)
        {
            var hrt = typeBadgeText.rectTransform;
            hrt.anchorMin = new Vector2(0.58f, 0.875f);
            hrt.anchorMax = new Vector2(0.95f, 0.90f);
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
        }
        // Turn count sits under the scene header, above the progress slider.
        if (turnsText != null)
        {
            var trt = turnsText.rectTransform;
            trt.anchorMin = new Vector2(0.05f, 0.848f);
            trt.anchorMax = new Vector2(0.95f, 0.872f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }
        // Non-decision body text sits just above the decision buttons.
        // When a key decision is present, this shows the decision title;
        // otherwise it shows ritual/sting/fashion info.
        if (bodyText != null)
        {
            var bdy = bodyText.rectTransform;
            bdy.anchorMin = new Vector2(0.05f, 0.33f);
            bdy.anchorMax = new Vector2(0.95f, 0.37f);
            bdy.offsetMin = Vector2.zero; bdy.offsetMax = Vector2.zero;
        }
        // (Decision container positioning is handled explicitly in ShowScene
        // per decision; no static repositioning here.)
    }

    /// <summary>
    /// Measures the synopsis + prose and stacks them by hand inside the
    /// scroll content. (Layout groups are not used: the builder's
    /// VerticalLayoutGroup collapses children to zero size.)
    ///
    /// CRITICAL: Uses EXPLICIT pixel widths (not stretched anchors) for the
    /// text rects. Stretched anchors can yield zero width if the canvas hasn't
    /// laid out yet (panel just activated), which makes preferredHeight return
    /// garbage and the scroll content too short — the ScrollRect then clamps
    /// as if at the bottom while text overflows invisibly below.
    /// </summary>
    private void LayoutScrollContent()
    {
        if (proseScrollRect == null || proseScrollRect.content == null) return;
        var contentRT = proseScrollRect.content;

        // Defensive: strip any layout components that fight manual positioning.
        var vlg = contentRT.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false;
        var csf = contentRT.GetComponent<ContentSizeFitter>();
        if (csf != null) csf.enabled = false;

        Canvas.ForceUpdateCanvases(); // viewport needs a real size before measuring
        var viewportRT = proseScrollRect.viewport as RectTransform;
        float viewW = viewportRT != null ? viewportRT.rect.width : 0f;
        if (viewW <= 1f)
        {
            // Panel just activated / canvas not laid out yet. Fall back to an
            // estimated portrait width so preferredHeight measures correctly.
            viewW = Screen.width * 0.9f;
            if (viewW <= 1f) viewW = 700f;
        }

        const float pad = 12f;
        const float spacing = 12f;
        float textW = Mathf.Max(viewW - pad * 2f, 100f);
        float y = -pad; // running top edge (pivot is top)

        // Position the content: top-anchored, full viewport width, explicit size.
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(0, 1);
        contentRT.pivot = new Vector2(0, 1);
        contentRT.anchoredPosition = Vector2.zero;

        if (synopsisText != null && !string.IsNullOrEmpty(synopsisText.text))
        {
            synopsisText.gameObject.SetActive(true);
            var srt = synopsisText.rectTransform;
            // Explicit top-left anchored rect: guaranteed valid width for measurement.
            srt.anchorMin = new Vector2(0, 1);
            srt.anchorMax = new Vector2(0, 1);
            srt.pivot = new Vector2(0, 1);
            srt.anchoredPosition = new Vector2(pad, y);
            srt.sizeDelta = new Vector2(textW, 10000f); // tall temp, shrinks to preferred
            Canvas.ForceUpdateCanvases();
            float h = Mathf.Max(synopsisText.preferredHeight, 20f);
            srt.sizeDelta = new Vector2(textW, h);
            y -= h + spacing;
        }
        else if (synopsisText != null)
        {
            synopsisText.gameObject.SetActive(false);
        }

        if (proseText != null)
        {
            var prt = proseText.rectTransform;
            prt.anchorMin = new Vector2(0, 1);
            prt.anchorMax = new Vector2(0, 1);
            prt.pivot = new Vector2(0, 1);
            prt.anchoredPosition = new Vector2(pad, y);
            prt.sizeDelta = new Vector2(textW, 10000f);
            Canvas.ForceUpdateCanvases();
            float h = Mathf.Max(proseText.preferredHeight, 20f);
            prt.sizeDelta = new Vector2(textW, h);
            y -= h + pad;
        }

        // Content size: full viewport width, height covering all stacked text.
        float contentH = Mathf.Max(-y, 20f);
        contentRT.sizeDelta = new Vector2(viewW, contentH);

        Canvas.ForceUpdateCanvases();
        proseScrollRect.verticalNormalizedPosition = 1f; // scroll to top
    }

    /// <summary>Loads and renders a scene. Returns false if the scene is missing.</summary>
    public bool ShowScene(int season, int chapter, int sceneNumber)
    {
        // Self-sufficient: ShowScene may be called synchronously right after
        // the panel is activated (Unity runs Awake on SetActive but defers
        // Start until before the first Update), so never assume Start ran.
        if (_game == null)
            _game = GameManager.Instance;
        EnsurePortraitUI();
        WireFooterButtons();

        _scene = _game != null ? _game.GetScene(season, chapter, sceneNumber) : null;
        if (_scene == null) return false;

        _selectedDecisionIndex = -1;
        ClearDecisionButtons();

        if (chapterTitleText != null)
        {
            chapterTitleText.text = $"S{_scene.season} · Chapter {_scene.chapter}: {_scene.chapterTitle}";
            chapterTitleText.color = goldAccent;
        }
        if (sceneHeaderText != null)
        {
            sceneHeaderText.text = $"Scene {_scene.sceneNumber} of 40";
            sceneHeaderText.color = lightTextColor;
        }
        if (typeBadgeText != null)
        {
            typeBadgeText.text = TypeLabel(_scene.type);
            typeBadgeText.color = goldAccent;
        }
        if (turnsText != null)
        {
            turnsText.text = _scene.playerTurns > 0
                ? $"{_scene.playerTurns} turns"
                : string.Empty;
            turnsText.color = dimTextColor;
        }
        if (progressSlider != null)
        {
            progressSlider.minValue = 0;
            progressSlider.maxValue = 40;
            progressSlider.value = _scene.sceneNumber;
        }
        if (synopsisText != null)
        {
            synopsisText.text = _scene.synopsis;
            synopsisText.color = dimTextColor;
        }
        if (proseText != null)
        {
            proseText.text = _scene.prose;
            proseText.color = lightTextColor;
        }
        if (proseScrollRect != null)
            proseScrollRect.verticalNormalizedPosition = 1f; // scroll to top
        LayoutScrollContent();

        // Decisions: tappable buttons. Non-decision content goes to bodyText.
        if (_scene.keyDecision != null && _scene.keyDecision.options != null && _scene.keyDecision.options.Length > 0)
        {
            // Size the decision container explicitly to fit the fixed-height
            // buttons exactly: bottom-anchored, 100px above the panel bottom
            // (clear of the footer), explicit pixel height. This prevents any
            // percentage/pixel mismatch from overflowing onto the footer.
            var containerRT = decisionButtonContainer as RectTransform;
            if (containerRT != null)
            {
                int optCount = _scene.keyDecision.options.Length;
                const float btnH = 64f;
                const float btnSpacing = 8f;
                float totalH = optCount * btnH + (optCount - 1) * btnSpacing;
                containerRT.anchorMin = new Vector2(0.05f, 0f);
                containerRT.anchorMax = new Vector2(0.95f, 0f);
                containerRT.pivot = new Vector2(0.5f, 0f);
                containerRT.anchoredPosition = new Vector2(0f, 100f);
                containerRT.sizeDelta = new Vector2(0f, totalH);
            }
            SpawnDecisionButtons(_scene);
            if (bodyText != null)
            {
                bodyText.text = $"Decision {_scene.keyDecision.number}: {_scene.keyDecision.title}";
                bodyText.color = goldAccent;
            }
        }
        else if (bodyText != null)
        {
            bodyText.text = BuildBody(_scene);
            bodyText.color = lightTextColor;
        }

        // Restore a previously-made decision selection for this scene, if any.
        int saved = _game.GetDecisionChoice(season, chapter, sceneNumber);
        if (saved >= 0)
            SelectDecision(saved, false);

        UpdateContinueLabel();

        // Background: rotate through approved estate plates by chapter.
        // The background must NEVER block raycasts — a full-screen Image with
        // raycastTarget=true swallows taps meant for the footer buttons
        // (v9.6: Scene 4 Continue intermittently dead across sessions).
        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = false;
            if (estatePlates != null && estatePlates.Length > 0)
            {
                int idx = (_scene.chapter - 1) % estatePlates.Length;
                var plate = estatePlates[idx];
                if (plate != null)
                {
                    backgroundImage.sprite = plate;
                    backgroundImage.color = new Color(1f, 1f, 1f, 0.25f);
                }
            }
        }

        // Footer buttons must be last in sibling order (on top) so nothing
        // created earlier can cover them and swallow taps.
        if (continueButton != null) continueButton.transform.SetAsLastSibling();
        if (toBoardButton != null) toBoardButton.transform.SetAsLastSibling();

        return true;
    }

    private static string TypeLabel(SceneType type)
    {
        switch (type)
        {
            case SceneType.Dialogue: return "Dialogue";
            case SceneType.FashionSelection: return "Fashion selection";
            case SceneType.ChapterClimax: return "Dressing ritual";
            case SceneType.Texture: return "Texture";
            case SceneType.PlotBeat: return "Story beat";
            case SceneType.GazetteSting: return "Gazette sting";
            case SceneType.Cliffhanger: return "Cliffhanger";
            default: return type.ToString();
        }
    }

    private void ClearDecisionButtons()
    {
        foreach (var b in _spawnedDecisionButtons)
            if (b != null) Destroy(b.gameObject);
        _spawnedDecisionButtons.Clear();
    }

    private void SpawnDecisionButtons(SceneDefinitionSO scene)
    {
        if (decisionButtonContainer == null || decisionButtonPrefab == null) return;
        // Manual vertical stack (no layout group — see EnsurePortraitUI).
        //
        // FIX (v9.7): Buttons use a FIXED pixel height and the container is
        // sized explicitly in ShowScene to fit them exactly. The v9.6 approach
        // measured the container's rect height (often 0 before first layout)
        // and fell back to an estimate — on short panels the estimate was too
        // tall, buttons overflowed the container, covered the footer, and the
        // Continue button became untappable (Scene 9 dead-end).
        var options = scene.keyDecision.options;
        const float buttonH = 64f;
        const float spacing = 8f;
        int count = options.Length;
        for (int i = 0; i < count; i++)
        {
            int idx = i; // capture
            var btn = Instantiate(decisionButtonPrefab, decisionButtonContainer);
            btn.gameObject.SetActive(true); // Instantiate preserves the template's inactive state
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            float top = i * (buttonH + spacing);
            rt.offsetMin = new Vector2(0, -(top + buttonH));
            rt.offsetMax = new Vector2(0, -top);
            var label = btn.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = $"{(char)('A' + i)}. {options[i].label}";
                label.color = lightTextColor;
            }
            btn.onClick.AddListener(() => SelectDecision(idx, true));
            _spawnedDecisionButtons.Add(btn);
        }
    }

    private void SelectDecision(int index, bool persist)
    {
        _selectedDecisionIndex = index;
        for (int i = 0; i < _spawnedDecisionButtons.Count; i++)
        {
            var btn = _spawnedDecisionButtons[i];
            if (btn == null) continue;
            var colors = btn.colors;
            // Highlight selected: gold-tinted; others default.
            colors.normalColor = (i == index)
                ? new Color(0.45f, 0.36f, 0.18f, 1f)
                : new Color(0.22f, 0.28f, 0.22f, 1f);
            colors.selectedColor = colors.normalColor;
            btn.colors = colors;
        }
        if (persist && _scene != null)
            _game.RecordDecision(_scene.season, _scene.chapter, _scene.sceneNumber, index);
        UpdateContinueLabel();
    }

    private void UpdateContinueLabel()
    {
        if (continueButton == null) return;
        var label = continueButton.GetComponentInChildren<Text>();
        if (label == null) return;
        bool needsDecision = _scene != null
            && _scene.keyDecision != null
            && _scene.keyDecision.options != null
            && _scene.keyDecision.options.Length > 0
            && _selectedDecisionIndex < 0;
        label.text = needsDecision ? "Choose above to continue" : "Continue →";
    }

    /// <summary>
    /// Builds the non-decision body from structured data only.
    /// Rituals show the brief, directions, and pin counts; stings show
    /// Bell's verdict; fashion choices list remembered picks.
    /// </summary>
    private static string BuildBody(SceneDefinitionSO scene)
    {
        var sb = new System.Text.StringBuilder();

        if (scene.ritual != null)
        {
            var r = scene.ritual;
            if (!string.IsNullOrEmpty(r.part))
                sb.AppendLine(r.part);
            if (!string.IsNullOrEmpty(r.occasionBrief))
            {
                sb.AppendLine("Occasion:");
                sb.AppendLine(r.occasionBrief);
                sb.AppendLine();
            }
            if (r.directions != null && r.directions.Length > 0)
            {
                sb.AppendLine("Directions:");
                char d = 'A';
                foreach (var dir in r.directions)
                {
                    sb.AppendLine($"{d}. {dir}");
                    d++;
                }
                sb.AppendLine();
            }
            if (!string.IsNullOrEmpty(r.stepsSummary))
                sb.AppendLine(r.stepsSummary);
            if (r.steps != null && r.steps.Length > 0)
                sb.AppendLine($"{r.steps.Length} ritual pins · {r.coinPerDecision} coins each");
            else if (r.coinTotal > 0)
                sb.AppendLine($"Ritual total: {r.coinTotal} coins");
        }

        if (scene.fashionChoices != null && scene.fashionChoices.Length > 0)
        {
            sb.AppendLine("Fashion choices (remembered, no coin cost):");
            foreach (var f in scene.fashionChoices)
                sb.AppendLine($"• {f.label}");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(scene.sting))
        {
            sb.AppendLine("— The Gazette —");
            sb.AppendLine(scene.sting);
        }

        if (scene.participants != null && scene.participants.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("With: " + string.Join(", ", scene.participants));
        }

        string result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result)
            ? "Play the scene — full prose is in the chapter text."
            : result;
    }

    private void OnContinue()
    {
        // A key decision must be chosen before advancing.
        if (_scene != null && _scene.keyDecision != null
            && _scene.keyDecision.options != null && _scene.keyDecision.options.Length > 0
            && _selectedDecisionIndex < 0)
            return;

        // Story scenes cost coins, never Crowns (locked). Price is computed at
        // import per scene. Energy is the only throttle; plot is never time-gated.
        // Insufficient funds NEVER route away: the player stays on the story
        // and is told how to earn coins. (No surprise trips to the merge board.)
        if (!_game.TryPaySceneCost(_scene))
        {
            if (bodyText != null)
            {
                bodyText.text = "Not enough coins — earn them on the merge board, then continue the story.";
                bodyText.color = lightTextColor;
            }
            return;
        }
        _game.AdvanceStory();
        if (!ShowScene(_game.CurrentSeason, _game.CurrentChapter, _game.CurrentSceneNumber))
        {
            // No more authored scenes — stay on the last one, do not reroute.
            if (bodyText != null)
            {
                bodyText.text = "To be continued — the next scene isn't written yet.";
                bodyText.color = lightTextColor;
            }
        }
    }
}
