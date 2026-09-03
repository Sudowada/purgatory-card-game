using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.InputSystem;

/// <summary>
/// ESC opens/closes this settings panel.
/// Tabs: Settings (brightness + volume sliders) and Journal (lore entries).
/// Auto-builds its own UI on Start.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    [Header("Fonts")]
    [SerializeField] public TMP_FontAsset headerFont;
    [SerializeField] public TMP_FontAsset bodyFont;

    // ── Palette ───────────────────────────────────────────────────────────────
    static Color Parch      => new Color(0.82f, 0.73f, 0.52f);
    static Color ParchLight => new Color(0.90f, 0.82f, 0.62f);
    static Color ParchDim   => new Color(0.52f, 0.42f, 0.26f);
    static Color Blood      => new Color(0.68f, 0.06f, 0.05f);
    static Color Rust       => new Color(0.40f, 0.16f, 0.05f);
    static Color IronDark   => new Color(0.11f, 0.10f, 0.09f);

    // ── State ─────────────────────────────────────────────────────────────────
    private GameObject   _root;
    private CanvasGroup  _cg;
    private bool         _open;

    private GameObject   _settingsTab;
    private GameObject   _journalTab;

    // Journal
    private List<string>    _journalKeys = new List<string>();
    private int             _journalPage;
    private TextMeshProUGUI _journalBody;
    private TextMeshProUGUI _journalCounter;

    // Brightness overlay
    private Image _dimOverlay;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

   void Start()
{
    // Borrow fonts from HUDBuilder if not manually assigned
    if (headerFont == null || bodyFont == null)
    {
        var hud = FindFirstObjectByType<HUDBuilder>();
        if (hud != null)
        {
            if (headerFont == null) headerFont = hud.headerFont;
            if (bodyFont == null)   bodyFont   = hud.bodyFont;
        }
    }
    BuildBrightnessOverlay();
    BuildUI();
        _root.SetActive(false);

        // Apply saved prefs
        float br = PlayerPrefs.GetFloat("Brightness", 1f);
        float mv = PlayerPrefs.GetFloat("MasterVol",  1f);
        float sv = PlayerPrefs.GetFloat("SFXVol",     1f);
        ApplyBrightness(br);
        ApplyMasterVol(mv);
        ApplySFXVol(sv);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_open) Close(); else Open();
        }
    }

    // ── OPEN / CLOSE ──────────────────────────────────────────────────────────
    public void Open()
    {
        if (_open) return;
        _open = true;
        RefreshJournalKeys();
        _root.SetActive(true);
        _cg.alpha = 0f;
        _cg.DOFade(1f, 0.2f);
        _root.transform.localScale = Vector3.one * 0.95f;
        _root.transform.DOScale(Vector3.one, 0.22f).SetEase(Ease.OutBack);
        GameManager.Instance?.SetInputBlocked(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        _cg.DOFade(0f, 0.15f).OnComplete(() => _root.SetActive(false));
        GameManager.Instance?.SetInputBlocked(false);
    }

    // ── APPLY SETTINGS ────────────────────────────────────────────────────────
    void ApplyBrightness(float v)
    {
        if (_dimOverlay != null)
            _dimOverlay.color = new Color(0, 0, 0, (1f - v) * 0.85f);
        PlayerPrefs.SetFloat("Brightness", v);
    }

    void ApplyMasterVol(float v)
    {
        AudioListener.volume = v;
        if (AudioManager.Instance != null) AudioManager.Instance.masterVolume = v;
        PlayerPrefs.SetFloat("MasterVol", v);
    }

    void ApplySFXVol(float v)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.sfxVolume = v;
        PlayerPrefs.SetFloat("SFXVol", v);
    }

    // ── JOURNAL ───────────────────────────────────────────────────────────────
    void RefreshJournalKeys()
    {
        _journalKeys.Clear();
        var ds = DocumentSystem.Instance;
        if (ds == null) return;
        foreach (var k in ds.AllUnlocked.Keys) _journalKeys.Add(k);
        string[] order = { "♥", "♠", "♦", "♣" };
        _journalKeys.Sort((a, b) =>
        {
            int sa = System.Array.IndexOf(order, Suit(a));
            int sb = System.Array.IndexOf(order, Suit(b));
            return sa != sb ? sa.CompareTo(sb) : Val(a).CompareTo(Val(b));
        });
        _journalPage = Mathf.Clamp(_journalPage, 0, Mathf.Max(0, _journalKeys.Count - 1));
    }

    string Suit(string id)
    {
        foreach (var s in new[] { "♥", "♠", "♦", "♣" }) if (id.Contains(s)) return s;
        return "z";
    }
    int Val(string id)
    {
        var p = id.Split('_');
        return p.Length > 0 && int.TryParse(p[p.Length - 1], out int v) ? v : 0;
    }

    void ShowPage(int idx)
    {
        if (_journalBody == null) return;
        if (_journalKeys.Count == 0)
        {
            _journalBody.text = "<i>No entries yet.\n\nLet the cards decay.</i>";
            if (_journalCounter != null) _journalCounter.text = "0 / 0";
            return;
        }
        _journalPage = Mathf.Clamp(idx, 0, _journalKeys.Count - 1);
        var ds = DocumentSystem.Instance;
        _journalBody.text = ds != null && ds.AllUnlocked.TryGetValue(
            _journalKeys[_journalPage], out string t) ? t : "???";
        if (_journalCounter != null)
            _journalCounter.text = $"{_journalPage + 1} / {_journalKeys.Count}";
    }

    // ── BUILD UI ──────────────────────────────────────────────────────────────
    void BuildUI()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Fullscreen root
        _root = Go("SettingsRoot", canvas.transform);
        Fill(_root);
        _root.transform.SetAsLastSibling();
        Img(_root, new Color(0, 0, 0, 0.80f));
        _cg = _root.AddComponent<CanvasGroup>();
        // Backdrop click closes
        var bb = _root.AddComponent<Button>();
        bb.targetGraphic = _root.GetComponent<Image>();
        bb.onClick.AddListener(Close);

        // Window 720 × 520
        var win = Go("Window", _root.transform);
        AnchorCenter(win, 720, 520);
        Img(win, IronDark);
        // Block backdrop close (reuse win's own Image — don't add a second one)
        var wb = win.AddComponent<Button>();
        wb.targetGraphic = win.GetComponent<Image>();

        // Inner panel inset 5px
        var inner = Go("Inner", win.transform);
        Inset(inner, 5);
        Img(inner, new Color(0.10f, 0.08f, 0.06f, 1f));

        // Rust top stripe
        var stripe = Go("Stripe", inner.transform);
        TopStripe(stripe, 3);
        Img(stripe, Rust);

        // Title
        var title = Go("Title", inner.transform);
        FromTop(title, 6, 700, 44);
        Txt(title, "P U R G A T O R Y", 18f, Blood, TextAlignmentOptions.Center,
            FontStyles.Bold | FontStyles.SmallCaps, headerFont);

        // Divider
        var div = Go("Div", inner.transform);
        FromTop(div, 52, 680, 1);
        Img(div, new Color(ParchDim.r, ParchDim.g, ParchDim.b, 0.35f));

        // Tab buttons row — 56px from top, 34px tall
        var tabRow = Go("TabRow", inner.transform);
        FromTop(tabRow, 55, 700, 34);

        var stBtn = TabBtn("  Settings  ", tabRow.transform, 0f,   0.5f);
        var jlBtn = TabBtn("  Journal  ",  tabRow.transform, 0.5f, 1.0f);

        // Content area — everything below tabs, above bottom padding
        var content = Go("Content", inner.transform);
        var cRT = content.GetComponent<RectTransform>(); // Go() already created this — fetch, don't re-add
        cRT.anchorMin = Vector2.zero;
        cRT.anchorMax = Vector2.one;
        cRT.offsetMin = new Vector2(24f, 24f);
        cRT.offsetMax = new Vector2(-24f, -96f);

        // Settings tab
        _settingsTab = Go("SettingsTab", content.transform);
        Fill(_settingsTab);
        BuildSettingsTab(_settingsTab.transform);

        // Journal tab
        _journalTab = Go("JournalTab", content.transform);
        Fill(_journalTab);
        BuildJournalTab(_journalTab.transform);
        _journalTab.SetActive(false);

        // Tab button listeners
        stBtn.onClick.AddListener(() => { _settingsTab.SetActive(true);  _journalTab.SetActive(false); });
        jlBtn.onClick.AddListener(() => { _settingsTab.SetActive(false); _journalTab.SetActive(true);
                                          RefreshJournalKeys(); ShowPage(_journalPage); });

        // Close X
        var x = Go("CloseX", inner.transform);
        TopRight(x, 8, 30, 30);
        Img(x, new Color(Blood.r, Blood.g, Blood.b, 0.18f));
        var xLabelGO = Go("Label", x.transform);
        Fill(xLabelGO);
        Txt(xLabelGO, "X", 14f, ParchDim, TextAlignmentOptions.Center);
        var xb = x.AddComponent<Button>();
        xb.targetGraphic = x.GetComponent<Image>();
        xb.onClick.AddListener(Close);
    }

    void BuildSettingsTab(Transform p)
    {
        float y = 0f;
        const float GAP = 70f;

        y = BuildSlider(p, "Brightness",     y, GAP,
            PlayerPrefs.GetFloat("Brightness", 1f), ApplyBrightness);
        y = BuildSlider(p, "Master Volume",  y, GAP,
            PlayerPrefs.GetFloat("MasterVol",  1f), ApplyMasterVol);
        y = BuildSlider(p, "SFX Volume",     y, GAP,
            PlayerPrefs.GetFloat("SFXVol",     1f), ApplySFXVol);

        // Resume button
        var resumeGO = Go("Resume", p);
        FromTop(resumeGO, y + 20f, 180, 44);
        Img(resumeGO, new Color(0.15f, 0.38f, 0.09f, 0.95f));
        var resumeLabelGO = Go("Label", resumeGO.transform);
        Fill(resumeLabelGO);
        Txt(resumeLabelGO, "Resume", 15f, ParchLight, TextAlignmentOptions.Center,
            FontStyles.SmallCaps, headerFont);
        var rb = resumeGO.AddComponent<Button>();
        rb.targetGraphic = resumeGO.GetComponent<Image>();
        rb.onClick.AddListener(Close);
    }

    // Returns new y offset after this row
    float BuildSlider(Transform parent, string label, float yTop, float rowH,
                      float initVal, System.Action<float> onChange)
    {
        var row = Go(label + "Row", parent);
        FromTop(row, yTop, 0, rowH); // full width, rowH tall
        row.GetComponent<RectTransform>().offsetMin = new Vector2(0, row.GetComponent<RectTransform>().offsetMin.y);
        row.GetComponent<RectTransform>().offsetMax = new Vector2(0, row.GetComponent<RectTransform>().offsetMax.y);

        // Label (left 35%)
        var lbl = Go("Lbl", row.transform);
        var lRT = lbl.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0f);
        lRT.anchorMax = new Vector2(0.32f, 1f);
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        Txt(lbl, label.ToUpper(), 11f, ParchDim, TextAlignmentOptions.MidlineRight,
            FontStyles.SmallCaps, bodyFont);

        // Percent display (right 12%)
        var pct = Go("Pct", row.transform);
        var pRT = pct.GetComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.88f, 0f);
        pRT.anchorMax = new Vector2(1f,    1f);
        pRT.offsetMin = pRT.offsetMax = Vector2.zero;
        var pctTMP = Txt(pct, $"{Mathf.RoundToInt(initVal * 100)}%", 11f, Parch,
            TextAlignmentOptions.MidlineLeft, FontStyles.Normal, bodyFont);

        // Slider (middle 54%)
        var slGO = Go("Slider", row.transform);
        var sRT = slGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0.33f, 0.2f);
        sRT.anchorMax = new Vector2(0.87f, 0.8f);
        sRT.offsetMin = sRT.offsetMax = Vector2.zero;

        var sl = slGO.AddComponent<Slider>();
        sl.minValue = 0f; sl.maxValue = 1f; sl.value = initVal;

        // Background track
        var bg = Go("BG", slGO.transform);
        Fill(bg);
        Img(bg, new Color(0.06f, 0.05f, 0.04f, 0.9f));

        // Fill area
        var fa = Go("FillArea", slGO.transform);
        var faRT = fa.GetComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0f, 0.35f);
        faRT.anchorMax = new Vector2(1f, 0.65f);
        faRT.offsetMin = new Vector2(6f, 0f);
        faRT.offsetMax = new Vector2(-6f, 0f);

        var fillGO = Go("Fill", fa.transform);
        Fill(fillGO);
        var fillImg = Img(fillGO, Rust);

        // Handle
        var ha = Go("HandleArea", slGO.transform);
        Fill(ha);

        var hGO = Go("Handle", ha.transform);
        var hRT = hGO.GetComponent<RectTransform>();
        hRT.anchorMin = hRT.anchorMax = new Vector2(0.5f, 0.5f);
        hRT.sizeDelta = new Vector2(20f, 20f);
        var hImg = Img(hGO, ParchLight);

        sl.fillRect   = fillGO.GetComponent<RectTransform>();
        sl.handleRect = hGO.GetComponent<RectTransform>();
        sl.targetGraphic = hImg;

        sl.onValueChanged.AddListener(v =>
        {
            pctTMP.text = $"{Mathf.RoundToInt(v * 100)}%";
            onChange?.Invoke(v);
        });

        return yTop + rowH;
    }

    void BuildJournalTab(Transform p)
    {
        // Prisoner header labels
        var hdr = Go("Header", p);
        var hRT = hdr.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot     = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = Vector2.zero;
        hRT.sizeDelta = new Vector2(0f, 28f);
        Txt(hdr, "♥ Cyra     ♠ Bram     ♦ The Visitor     ♣ Uriel",
            9f, new Color(ParchDim.r, ParchDim.g, ParchDim.b, 0.65f),
            TextAlignmentOptions.Center, FontStyles.Italic, bodyFont);

        // Scroll area
        var scrollGO = Go("ScrollArea", p);
        var sRT = scrollGO.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.12f);
        sRT.anchorMax = new Vector2(1f, 1f);
        sRT.offsetMin = new Vector2(0f,  30f);
        sRT.offsetMax = new Vector2(0f, -2f);

        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.scrollSensitivity = 20f;

        var vp = Go("Viewport", scrollGO.transform);
        Fill(vp);
        var vpMask = vp.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        vp.AddComponent<Image>().color = Color.clear;

        var ct = Go("EntryText", vp.transform);
        var ctRT = ct.GetComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0f, 1f);
        ctRT.anchorMax = new Vector2(1f, 1f);
        ctRT.pivot     = new Vector2(0.5f, 1f);
        ctRT.anchoredPosition = Vector2.zero;
        ctRT.sizeDelta = new Vector2(0f, 400f);
        var csf = ct.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _journalBody = ct.AddComponent<TextMeshProUGUI>();
        _journalBody.fontSize  = 13f;
        _journalBody.color     = new Color(0.85f, 0.78f, 0.60f);
        _journalBody.fontStyle = FontStyles.Italic;
        _journalBody.textWrappingMode = TextWrappingModes.Normal;
        _journalBody.text = "<i>No entries yet.\n\nLet the cards decay.</i>";
        if (bodyFont) _journalBody.font = bodyFont;

        sr.content  = ctRT;
        sr.viewport = vp.GetComponent<RectTransform>();

        // Nav bar at bottom
        var nav = Go("Nav", p);
        var nRT = nav.GetComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0f, 0f);
        nRT.anchorMax = new Vector2(1f, 0.12f);
        nRT.offsetMin = nRT.offsetMax = Vector2.zero;
        Img(nav, new Color(0.06f, 0.05f, 0.03f, 0.7f));

        NavBtn("← Prev", nav.transform, 0f, 0.28f).onClick.AddListener(
            () => ShowPage(_journalPage - 1));

        var ctrGO = Go("Counter", nav.transform);
        var cRT2  = ctrGO.GetComponent<RectTransform>();
        cRT2.anchorMin = new Vector2(0.29f, 0f);
        cRT2.anchorMax = new Vector2(0.71f, 1f);
        cRT2.offsetMin = cRT2.offsetMax = Vector2.zero;
        _journalCounter = Txt(ctrGO, "0 / 0", 11f, ParchDim, TextAlignmentOptions.Center,
            FontStyles.Normal, bodyFont);

        NavBtn("Next →", nav.transform, 0.72f, 1f).onClick.AddListener(
            () => ShowPage(_journalPage + 1));
    }

    // ── BRIGHTNESS OVERLAY ────────────────────────────────────────────────────
    void BuildBrightnessOverlay()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        var go = Go("BrightnessOverlay", canvas.transform);
        go.transform.SetSiblingIndex(0);
        Fill(go);
        _dimOverlay = Img(go, Color.clear);
        _dimOverlay.raycastTarget = false;
    }

    // ── UI HELPERS ────────────────────────────────────────────────────────────

    GameObject Go(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>(); // always needed for UI
        return go;
    }

    // Full stretch fill
    void Fill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Centred fixed size
    void AnchorCenter(GameObject go, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    // Inset all sides equally
    void Inset(GameObject go, float px)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(px, px);
        rt.offsetMax = new Vector2(-px, -px);
    }

    // Thin stripe at top
    void TopStripe(GameObject go, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0f, h);
    }

    // Fixed height anchored from top, full width
    void FromTop(GameObject go, float fromTop, float width, float height)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -fromTop);
        rt.sizeDelta = new Vector2(width > 0 ? width : 0f, height);
        if (width <= 0)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        }
    }

    // Fixed size at top-right
    void TopRight(GameObject go, float margin, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-margin, -margin);
        rt.sizeDelta = new Vector2(w, h);
    }

    Image Img(GameObject go, Color col)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = col;
        return img;
    }

    TextMeshProUGUI Txt(GameObject go, string text, float size, Color col,
        TextAlignmentOptions align = TextAlignmentOptions.Center,
        FontStyles style = FontStyles.Normal, TMP_FontAsset font = null)
    {
        var t = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = col;
        t.alignment = align; t.fontStyle = style;
        if (font) t.font = font;
        return t;
    }

    Button TabBtn(string label, Transform parent, float xMin, float xMax)
    {
        var go = Go(label, parent);
        var rt = go.GetComponent<RectTransform>(); // Go() already created this — fetch, don't re-add
        rt.anchorMin = new Vector2(xMin, 0f); rt.anchorMax = new Vector2(xMax, 1f);
        rt.offsetMin = new Vector2(2f, 1f);   rt.offsetMax = new Vector2(-2f, -1f);
        Img(go, new Color(0.08f, 0.07f, 0.05f, 0.9f));

        // Label lives on a CHILD object — Image and TextMeshProUGUI are both
        // Graphic components and can't coexist on the same GameObject.
        var labelGO = Go("Label", go.transform);
        Fill(labelGO);
        Txt(labelGO, label.Trim().ToUpper(), 11f, ParchDim,
            TextAlignmentOptions.Center, FontStyles.SmallCaps, headerFont);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        return btn;
    }

    Button NavBtn(string label, Transform parent, float xMin, float xMax)
    {
        var go = Go(label, parent);
        var rt = go.GetComponent<RectTransform>(); // Go() already created this — fetch, don't re-add
        rt.anchorMin = new Vector2(xMin, 0f); rt.anchorMax = new Vector2(xMax, 1f);
        rt.offsetMin = new Vector2(4f, 4f);   rt.offsetMax = new Vector2(-4f, -4f);
        Img(go, new Color(0.10f, 0.08f, 0.05f, 0.85f));

        // Label lives on a CHILD object — same reason as TabBtn above.
        var labelGO = Go("Label", go.transform);
        Fill(labelGO);
        Txt(labelGO, label, 11f, ParchDim, TextAlignmentOptions.Center,
            FontStyles.Normal, bodyFont);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        return btn;
    }

    Sprite CircleSprite(int s)
    {
        var tex = new Texture2D(s, s);
        float r = s * 0.5f;
        var px = tex.GetPixels();
        for (int y = 0; y < s; y++)
            for (int x2 = 0; x2 < s; x2++)
            {
                float dx = x2 - r + .5f, dy = y - r + .5f;
                px[y * s + x2] = dx*dx + dy*dy <= r*r ? ParchLight : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0,0,s,s), Vector2.one*.5f);
    }
}