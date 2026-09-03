using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using System.Collections;

/// <summary>
/// Builds and shows the end-of-run stat screen.
/// Gothic parchment aesthetic — Inscription / Cloverpit style.
/// Called by GameOverSequence after fade to black.
/// </summary>
public class RunStatsUI : MonoBehaviour
{
    public static RunStatsUI Instance { get; private set; }

    [SerializeField] private TMP_FontAsset headerFont;
    [SerializeField] private TMP_FontAsset bodyFont;

    // Palette
    static readonly Color c_parchment  = new Color(0.88f, 0.80f, 0.62f);
    static readonly Color c_parchDark  = new Color(0.55f, 0.44f, 0.28f);
    static readonly Color c_burnt      = new Color(0.14f, 0.09f, 0.04f);
    static readonly Color c_blood      = new Color(0.55f, 0.04f, 0.04f);
    static readonly Color c_ironDark   = new Color(0.12f, 0.11f, 0.10f);
    static readonly Color c_gold       = new Color(0.78f, 0.61f, 0.15f);
    static readonly Color c_dim        = new Color(0.45f, 0.38f, 0.25f);

    private Canvas      statsCanvas;
    private CanvasGroup rootGroup;
    private bool        built = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(this);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show()
    {
        if (!built) BuildScreen();
        statsCanvas.gameObject.SetActive(true);
        rootGroup.alpha = 0f;
        rootGroup.DOFade(1f, 1.2f).SetEase(Ease.InOutSine);
        StartCoroutine(AnimateStatRows());
    }

    // ── Build UI ──────────────────────────────────────────────────────────────

    void BuildScreen()
    {
        built = true;

        // Root canvas
        var canvasGO = new GameObject("RunStatsCanvas");
        statsCanvas  = canvasGO.AddComponent<Canvas>();
        statsCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        statsCanvas.sortingOrder = 99;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        rootGroup       = canvasGO.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;

        // Full screen dark overlay
        MakeImage(canvasGO.transform, "BG", new Color(0.03f, 0.02f, 0.01f, 0.97f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Parchment panel — centred, tall
        var panel = MakeParchmentPanel(canvasGO.transform);

        // Build contents inside panel
        BuildContents(panel);

        statsCanvas.gameObject.SetActive(false);
    }

    RectTransform MakeParchmentPanel(Transform parent)
    {
        var go  = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(520f, 680f);
        rt.anchoredPosition = Vector2.zero;

        // Parchment texture background
        var img = go.AddComponent<Image>();
        img.color = c_parchment;

        // Procedural parchment texture
        var tex = MakeParchmentTex(256, 320);
        var sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        img.sprite = sprite;
        img.type   = Image.Type.Sliced;

        // Dark border frame
        MakeImage(go.transform, "Border",
            new Color(c_burnt.r, c_burnt.g, c_burnt.b, 0.85f),
            Vector2.zero, Vector2.one, new Vector2(8,8), new Vector2(-8,-8));

        // Inner border line
        MakeImage(go.transform, "BorderInner",
            new Color(c_parchDark.r, c_parchDark.g, c_parchDark.b, 0.5f),
            Vector2.zero, Vector2.one, new Vector2(16,16), new Vector2(-16,-16));

        return rt;
    }

    void BuildContents(RectTransform panel)
    {
        float panelH = panel.sizeDelta.y;
        float panelW = panel.sizeDelta.x;

        // ── Skull header ──────────────────────────────────────────────────────
        MakeTMP(panel, "SkullIcon", "☠",
            new Vector2(0f, panelH * 0.5f - 48f),
            new Vector2(panelW - 40f, 60f),
            52f, c_blood, TextAlignmentOptions.Center, FontStyles.Normal);

        // ── Title ─────────────────────────────────────────────────────────────
        var title = MakeTMP(panel, "Title", "CONDEMNED",
            new Vector2(0f, panelH * 0.5f - 105f),
            new Vector2(panelW - 40f, 52f),
            38f, c_blood, TextAlignmentOptions.Center, FontStyles.Bold);
        if (headerFont && title) title.font = headerFont;

        // ── Subtitle ──────────────────────────────────────────────────────────
        var sub = MakeTMP(panel, "Subtitle", "Your soul has been claimed.",
            new Vector2(0f, panelH * 0.5f - 148f),
            new Vector2(panelW - 60f, 28f),
            13f, c_dim, TextAlignmentOptions.Center, FontStyles.Italic);
        if (bodyFont && sub) sub.font = bodyFont;

        // ── Divider ───────────────────────────────────────────────────────────
        MakeDivider(panel, panelH * 0.5f - 170f, panelW);

        // ── Stat rows ─────────────────────────────────────────────────────────
        RunStats s   = RunStats.Instance;
        float rowY   = panelH * 0.5f - 205f;
        float rowGap = 48f;

        var statRows = new (string icon, string label, string value)[]
        {
            ("⌛", "Time in Purgatory",  s != null ? s.FormattedDuration()          : "—"),
            ("◈",  "Rounds Survived",    s != null ? s.RoundsSurvived.ToString()    : "—"),
            ("✦",  "Hands Played",       s != null ? s.HandsPlayed.ToString()       : "—"),
            ("♠",  "Cards Played",       s != null ? s.CardsPlayed.ToString()       : "—"),
            ("⚔",  "Highest Hand",       s != null ? s.HighestHand.ToString()       : "—"),
            ("☠",  "Rotted Cards",       s != null ? s.DecayedCardsPlayed.ToString(): "—"),
            ("◎",  "Total Score",        s != null ? s.TotalScore.ToString()        : "—"),
            ("✦",  "Best Combo",         s != null ? s.BestComboName               : "—"),
        };

        foreach (var (icon, label, value) in statRows)
        {
            BuildStatRow(panel, icon, label, value, rowY, panelW);
            rowY -= rowGap;
        }

        // ── Bottom divider ────────────────────────────────────────────────────
        MakeDivider(panel, rowY + rowGap * 0.3f, panelW);

        // ── Flavour footer ────────────────────────────────────────────────────
        var footer = MakeTMP(panel, "Footer", "† Memento Mori †",
            new Vector2(0f, -panelH * 0.5f + 68f),
            new Vector2(panelW - 60f, 24f),
            11f, c_dim, TextAlignmentOptions.Center, FontStyles.Italic);
        if (bodyFont && footer) footer.font = bodyFont;

        // ── Rise Again button ─────────────────────────────────────────────────
        BuildRiseButton(panel, panelH);
    }

    void BuildStatRow(RectTransform parent, string icon, string label, string value,
                       float anchoredY, float panelW)
    {
        var row = new GameObject($"Row_{label}");
        row.transform.SetParent(parent, false);
        var rt = row.AddComponent<RectTransform>();
        rt.anchorMin       = new Vector2(0.5f, 0.5f);
        rt.anchorMax       = new Vector2(0.5f, 0.5f);
        rt.pivot           = new Vector2(0.5f, 0.5f);
        rt.sizeDelta       = new Vector2(panelW - 60f, 38f);
        rt.anchoredPosition = new Vector2(0f, anchoredY);

        // Row background tint (alternating subtle)
        var bg = row.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.06f);

        float innerW = panelW - 60f;

        // Icon
        MakeTMPLocal(row.transform, "Icon", icon,
            new Vector2(-innerW * 0.5f + 22f, 0f),
            new Vector2(30f, 36f),
            16f, c_gold, TextAlignmentOptions.Center, FontStyles.Normal);

        // Label
        var lbl = MakeTMPLocal(row.transform, "Label", label.ToUpper(),
            new Vector2(-innerW * 0.5f + 130f, 0f),
            new Vector2(220f, 36f),
            11f, c_parchDark, TextAlignmentOptions.Left, FontStyles.Normal);
        if (bodyFont && lbl) lbl.font = bodyFont;

        // Value — right-aligned, bright
        var val = MakeTMPLocal(row.transform, "Value", value,
            new Vector2(innerW * 0.5f - 20f, 0f),
            new Vector2(140f, 36f),
            14f, c_parchment, TextAlignmentOptions.Right, FontStyles.Bold);
        if (bodyFont && val) val.font = bodyFont;

        // Store reference for animation
        row.name = $"StatRow_{label}";
    }

    void BuildRiseButton(RectTransform parent, float panelH)
    {
        var btnGO = new GameObject("RiseButton");
        btnGO.transform.SetParent(parent, false);
        var rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(260f, 52f);
        rt.anchoredPosition = new Vector2(0f, -panelH * 0.5f + 36f);

        var bg = btnGO.AddComponent<Image>();
        bg.color = c_ironDark;

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = bg;

        // Hover colours
        var colors        = btn.colors;
        colors.normalColor      = c_ironDark;
        colors.highlightedColor = new Color(c_blood.r, c_blood.g, c_blood.b, 0.9f);
        colors.pressedColor     = new Color(0.3f, 0.02f, 0.02f);
        btn.colors = colors;

        // Label
        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(btnGO.transform, false);
        var lblRT = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text      = "✦  RISE AGAIN  ✦";
        lbl.fontSize  = 15f;
        lbl.color     = c_parchment;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.fontStyle = FontStyles.Bold;
        if (headerFont) lbl.font = headerFont;

        btn.onClick.AddListener(OnRiseAgain);

        // Blood border above
        MakeImage(btnGO.transform, "TopBorder",
            new Color(c_blood.r, c_blood.g, c_blood.b, 0.6f),
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            Vector2.zero, new Vector2(0f, 2f));
    }

    void OnRiseAgain()
    {
        // Fade out and reload scene
        var fadeGO = new GameObject("FadeOut");
        var c      = fadeGO.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 200;
        fadeGO.AddComponent<CanvasScaler>();
        var img   = new GameObject("Img").AddComponent<Image>();
        img.transform.SetParent(fadeGO.transform, false);
        var imgRT = img.GetComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero; imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = imgRT.offsetMax = Vector2.zero;
        img.color = new Color(0,0,0,0);
        img.DOFade(1f, 0.8f).OnComplete(() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
    }

    // ── Stat row stagger animation ─────────────────────────────────────────────

    IEnumerator AnimateStatRows()
    {
        yield return new WaitForSeconds(0.5f);

        // Find all stat rows and animate them in sequence
        var panel = statsCanvas.transform.GetChild(1); // parchment panel
        if (panel == null) yield break;

        foreach (Transform child in panel)
        {
            if (!child.name.StartsWith("StatRow_")) continue;

            var cg = child.GetComponent<CanvasGroup>();
            if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            Vector3 startPos = child.localPosition + new Vector3(-18f, 0f, 0f);
            child.localPosition = startPos;

            cg.DOFade(1f, 0.35f);
            child.DOLocalMoveX(child.localPosition.x + 18f, 0.35f).SetEase(Ease.OutCubic);

            yield return new WaitForSeconds(0.09f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void MakeDivider(RectTransform parent, float y, float panelW)
    {
        var go = new GameObject("Divider");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(panelW - 60f, 2f);
        rt.anchoredPosition = new Vector2(0f, y);
        var img = go.AddComponent<Image>();
        img.color = new Color(c_blood.r, c_blood.g, c_blood.b, 0.4f);
    }

    Image MakeImage(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI MakeTMP(RectTransform parent, string name, string text,
        Vector2 anchoredPos, Vector2 size, float fontSize, Color color,
        TextAlignmentOptions align, FontStyles style)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = anchoredPos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text; tmp.fontSize = fontSize;
        tmp.color     = color; tmp.alignment = align; tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    TextMeshProUGUI MakeTMPLocal(Transform parent, string name, string text,
        Vector2 anchoredPos, Vector2 size, float fontSize, Color color,
        TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = size;
        rt.anchoredPosition = anchoredPos;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text; tmp.fontSize = fontSize;
        tmp.color     = color; tmp.alignment = align; tmp.fontStyle = style;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    Texture2D MakeParchmentTex(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        Color light = new Color(0.94f, 0.87f, 0.68f);
        Color dark  = new Color(0.70f, 0.57f, 0.36f);
        Color burnt = new Color(0.18f, 0.12f, 0.06f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float n  = Mathf.PerlinNoise(x * 0.07f, y * 0.07f);
            float n2 = Mathf.PerlinNoise(x * 0.18f + 4f, y * 0.18f + 9f);
            Color c  = Color.Lerp(dark, light, n * 0.7f + n2 * 0.3f);
            // Edge burn
            float ex = Mathf.Min(x, w - 1 - x) / (float)(w * 0.12f);
            float ey = Mathf.Min(y, h - 1 - y) / (float)(h * 0.12f);
            float ed = Mathf.Min(ex, ey);
            float bn = Mathf.PerlinNoise(x * 0.11f + 3f, y * 0.11f + 7f);
            float bf = Mathf.Pow(Mathf.Clamp01(1f - ed), 1.8f) * Mathf.Lerp(0.3f, 1f, bn);
            c = Color.Lerp(c, burnt, bf * 0.9f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }
}
