// HUDBuilder v8 — Balatro-style left column + liquid tier bar + breakdown rows
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

[RequireComponent(typeof(Canvas))]
public class HUDBuilder : MonoBehaviour
{
    [Header("Fonts")]
    [SerializeField] public TMP_FontAsset headerFont;
    [SerializeField] public TMP_FontAsset bodyFont;

    [Header("Scoring Cell Material")]
    [Tooltip("Assign a material using your shader graph here — applied to POINTS, MULT, and SCORE boxes")]
    public Material scoringCellMaterial;

    static readonly Color c_parch      = new Color(0.82f, 0.73f, 0.52f, 1f);
    static readonly Color c_parchLight = new Color(0.90f, 0.82f, 0.62f, 1f);
    static readonly Color c_parchDim   = new Color(0.52f, 0.42f, 0.26f, 1f);
    static readonly Color c_blood      = new Color(0.68f, 0.06f, 0.05f, 1f);
    static readonly Color c_rust       = new Color(0.40f, 0.16f, 0.05f, 1f);
    static readonly Color c_gold       = new Color(0.78f, 0.60f, 0.12f, 1f);
    static readonly Color c_ironDark   = new Color(0.11f, 0.10f, 0.09f, 1f);
    static readonly Color c_blue       = new Color(0.30f, 0.52f, 0.88f, 1f);
    static readonly Color c_greenBtn   = new Color(0.15f, 0.38f, 0.09f, 1f);
    static readonly Color c_greenBtnHi = new Color(0.22f, 0.52f, 0.14f, 1f);
    static readonly Color c_panelBg    = new Color(0.07f, 0.06f, 0.04f, 0.96f);

    private Transform _pendingChipsMultParent;
    private float     _pendingChipsMultY;

    void Awake()
    {
        if (GetComponent<CanvasGroup>() == null)
            gameObject.AddComponent<CanvasGroup>();
    }

    void Start() => BuildHUD();

    void BuildHUD()
    {
        var scaler = GetComponent<CanvasScaler>() ?? gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        var (scoreText, roundText, enemyText, handsText, goldText) = BuildLeftColumn();
        var comboText = BuildChipsMult(_pendingChipsMultParent, _pendingChipsMultY);
        var playBtn   = BuildPlayButton();
        BuildJournalButton();

        var shopPanel = BuildShopPanel(
            out var relicName,  out var relicDesc,  out var relicCost,
            out var cardName,   out var cardRarity, out var cardCost,
            out var consumName, out var consumDesc, out var consumCost,
            out var burnCost,
            out var buyRelic, out var buyCard, out var buyConsumable,
            out var burnBtn, out var leaveBtn);

        var gameOverPanel = BuildGameOverPanel(out var gameOverScore);

        var gm = GameManager.Instance;
        if (gm == null) { Debug.LogError("HUDBuilder: no GameManager"); return; }
        gm.SetUIReferences(
            scoreText, roundText, enemyText, comboText, handsText, goldText, playBtn,
            shopPanel,
            relicName, relicDesc, relicCost,
            cardName,  cardRarity, cardCost,
            consumName, consumDesc, consumCost,
            burnCost, buyRelic, buyCard, buyConsumable, burnBtn, leaveBtn,
            gameOverPanel, gameOverScore, null);
    }

    (TextMeshProUGUI score, TextMeshProUGUI round,
     TextMeshProUGUI enemy, TextMeshProUGUI hands,
     TextMeshProUGUI gold) BuildLeftColumn()
    {
        const float CW = 420f;

        var col = R("LeftCol", transform,
            new Vector2(0f,0f), new Vector2(0f,1f),
            new Vector2(0f,0f), new Vector2(CW,0f));
        col.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        col.AddComponent<Image>().color = c_panelBg;

        var rb = R("RBorder", col.transform,
            new Vector2(1f,0f), new Vector2(1f,1f),
            Vector2.zero, new Vector2(2f,0f));
        rb.AddComponent<Image>().color = new Color(c_blood.r,c_blood.g,c_blood.b,0.45f);

        float pad = 20f;
        float IW  = CW - pad * 2f;
        float y   = 0f;

        // ── BLIND BOX ─────────────────────────────────────────────────────────
        float blindH = 140f;
        var blindBox = R("BlindBox", col.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, y), new Vector2(0f, blindH));
        blindBox.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
        blindBox.AddComponent<Image>().color = new Color(0.04f,0.03f,0.02f,1f);

        var blindLbl = TL("BlindLbl", blindBox.transform, "BLIND", 11f,
            new Vector2(pad,-10f), new Vector2(IW,14f), c_parchDim, bodyFont);
        blindLbl.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;

        var blindName = TL("BlindName", blindBox.transform, "The Rot",
            22f, new Vector2(pad,-24f), new Vector2(IW,22f), c_blood, headerFont);
        blindName.fontStyle = FontStyles.Bold;

        TL("ScoreNeeded", blindBox.transform, "Score 250",
            14f, new Vector2(pad,-46f), new Vector2(IW,18f),
            new Color(c_parchDim.r,c_parchDim.g,c_parchDim.b,0.75f), bodyFont);

        var barTrack = R("BarTrack", blindBox.transform,
            new Vector2(0f,0f), new Vector2(1f,0f),
            new Vector2(0f, 46f), new Vector2(-pad*2f, 12f));
        barTrack.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        barTrack.AddComponent<Image>().color = new Color(0.10f,0.08f,0.05f,1f);

        var fill = R("ScoreBar", barTrack.transform,
            new Vector2(0f,0f), new Vector2(0f,1f),
            Vector2.zero, new Vector2(0f,0f));
        fill.AddComponent<Image>().color = new Color(0.6f,0.13f,0.05f,1f);

        var barLabel = R("ScoreBarLabel", blindBox.transform,
            new Vector2(0f,0f), new Vector2(0.5f,0f),
            new Vector2(pad, 62f), new Vector2(-pad, 16f));
        barLabel.GetComponent<RectTransform>().pivot = new Vector2(0f,0f);
        var barLabelTMP = barLabel.AddComponent<TextMeshProUGUI>();
        barLabelTMP.text = "0 needed"; barLabelTMP.fontSize = 10f;
        barLabelTMP.alignment = TextAlignmentOptions.Left;
        barLabelTMP.color = new Color(c_parchDim.r,c_parchDim.g,c_parchDim.b,0.65f);
        barLabelTMP.raycastTarget = false;
        if (bodyFont) barLabelTMP.font = bodyFont;

        var targetVal = R("TargetVal", blindBox.transform,
            new Vector2(0.5f,0f), new Vector2(1f,0f),
            new Vector2(-pad, 62f), new Vector2(-pad, 16f));
        targetVal.GetComponent<RectTransform>().pivot = new Vector2(1f,0f);
        var targetTMP = targetVal.AddComponent<TextMeshProUGUI>();
        targetTMP.text = "/ 250"; targetTMP.fontSize = 10f;
        targetTMP.alignment = TextAlignmentOptions.Right;
        targetTMP.color = new Color(c_blue.r,c_blue.g,c_blue.b,0.75f);
        targetTMP.raycastTarget = false;
        if (bodyFont) targetTMP.font = bodyFont;

        y -= blindH;
        HDivider(col.transform, y, CW);

        // ── ROUND SCORE ───────────────────────────────────────────────────────
        float scoreH = 120f;
        var scoreSection = R("ScoreSection", col.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, y), new Vector2(0f, scoreH));
        scoreSection.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
        scoreSection.AddComponent<Image>().color = new Color(0.05f,0.04f,0.03f,1f);

        TL("ScoreLbl", scoreSection.transform, "ROUND SCORE", 12f,
            new Vector2(pad,-10f), new Vector2(IW,14f),
            new Color(c_parchDim.r,c_parchDim.g,c_parchDim.b,0.6f), bodyFont)
            .fontStyle = FontStyles.SmallCaps | FontStyles.Bold;

        var scoreText = TL("ScoreVal", scoreSection.transform, "0",
            56f, new Vector2(pad,-28f), new Vector2(IW,72f), c_parchLight, headerFont);
        scoreText.fontStyle = FontStyles.Bold;

        y -= scoreH;
        HDivider(col.transform, y, CW);

        // ── ROUND + ENEMY ─────────────────────────────────────────────────────
        float pillRowH = 100f;
        var pillRow = R("PillRow1", col.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, y), new Vector2(0f, pillRowH));
        pillRow.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);

        var roundText = Pill("RoundPill", pillRow.transform,
            "ROUND", "RoundVal", "1",
            new Vector2(0f,0f), new Vector2(0.5f,1f), c_parchLight);
        var enemyText = Pill("EnemyPill", pillRow.transform,
            "ENEMY CARD", "EnemyVal", "—",
            new Vector2(0.5f,0f), new Vector2(1f,1f), c_rust);

        var pd = R("PD", pillRow.transform,
            new Vector2(0.5f,0.1f), new Vector2(0.5f,0.9f),
            Vector2.zero, new Vector2(1f,0f));
        pd.AddComponent<Image>().color = new Color(1f,1f,1f,0.08f);

        y -= pillRowH;
        HDivider(col.transform, y, CW);

        // ── HANDS + GOLD ──────────────────────────────────────────────────────
        var pillRow2 = R("PillRow2", col.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, y), new Vector2(0f, pillRowH));
        pillRow2.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);

        var handsText = Pill("HandsPill", pillRow2.transform,
            "HANDS", "HandsVal", "5/5",
            new Vector2(0f,0f), new Vector2(0.5f,1f), new Color(0.22f,0.82f,0.35f));
        var goldText = Pill("GoldPill", pillRow2.transform,
            "GOLD", "GoldVal", "0g",
            new Vector2(0.5f,0f), new Vector2(1f,1f), c_gold);

        var pd2 = R("PD2", pillRow2.transform,
            new Vector2(0.5f,0.1f), new Vector2(0.5f,0.9f),
            Vector2.zero, new Vector2(1f,0f));
        pd2.AddComponent<Image>().color = new Color(1f,1f,1f,0.08f);

        y -= pillRowH;
        HDivider(col.transform, y, CW);

        _pendingChipsMultParent = col.transform;
        _pendingChipsMultY      = y;

        return (scoreText, roundText, enemyText, handsText, goldText);
    }

    TextMeshProUGUI Pill(string name, Transform parent,
        string label, string valueName, string initVal,
        Vector2 aMin, Vector2 aMax, Color valCol)
    {
        var p = R(name, parent, aMin, aMax, Vector2.zero, Vector2.zero);

        var lGO = R("L", p.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f,-8f), new Vector2(0f,18f));
        lGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
        var lt = lGO.AddComponent<TextMeshProUGUI>();
        lt.text = label; lt.fontSize = 12f;
        lt.alignment = TextAlignmentOptions.Center;
        lt.color = new Color(c_parchDim.r,c_parchDim.g,c_parchDim.b,0.6f);
        lt.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;
        lt.raycastTarget = false;
        if (bodyFont) lt.font = bodyFont;

        var vGO = R(valueName, p.transform,
            new Vector2(0f,0f), new Vector2(1f,1f),
            new Vector2(0f,0f), new Vector2(0f,-30f));
        var vt = vGO.AddComponent<TextMeshProUGUI>();
        vt.text = initVal; vt.fontSize = 36f;
        vt.alignment = TextAlignmentOptions.Center;
        vt.color = valCol; vt.fontStyle = FontStyles.Bold;
        vt.raycastTarget = false;
        if (headerFont) vt.font = headerFont;

        return vt;
    }

    void HDivider(Transform parent, float yPos, float w)
    {
        var go = R("HDiv", parent,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, yPos), new Vector2(0f,1f));
        go.AddComponent<Image>().color = new Color(1f,1f,1f,0.07f);
    }

    // =========================================================================
    // CHIPS × MULT — with liquid tier bar + breakdown rows
    // =========================================================================
    TextMeshProUGUI BuildChipsMult(Transform colTF = null, float yOffset = -500f)
    {
        if (colTF == null)
        {
            var colGO = GameObject.Find("LeftCol");
            colTF = colGO != null ? colGO.transform : transform;
        }

        const float SH = 370f;
        const float M  = 10f;

        var section = R("ChipsMultSection", colTF,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, yOffset), new Vector2(0f, SH));
        section.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
        section.AddComponent<Image>().color = new Color(0.04f,0.03f,0.02f,1f);

        R("TopLine", section.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            Vector2.zero, new Vector2(0f,1f))
            .AddComponent<Image>().color = new Color(c_blood.r,c_blood.g,c_blood.b,0.4f);

        // ── Combo header ───────────────────────────────────────────────────
        var hdrGO  = R("ChipsMultHdr", section.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f,-14f), new Vector2(0f,20f));
        var hdrTMP = hdrGO.AddComponent<TextMeshProUGUI>();
        hdrTMP.name = "ComboText"; hdrTMP.text = "Select cards";
        hdrTMP.fontSize = 14f; hdrTMP.alignment = TextAlignmentOptions.Center;
        hdrTMP.color = new Color(c_parchDim.r,c_parchDim.g,c_parchDim.b,0.75f);
        hdrTMP.fontStyle = FontStyles.Italic;
        if (bodyFont) hdrTMP.font = bodyFont;

        R("HDiv", section.transform,
            new Vector2(0.05f,1f), new Vector2(0.95f,1f),
            new Vector2(0f,-36f), new Vector2(0f,1f))
            .AddComponent<Image>().color = new Color(1f,1f,1f,0.05f);

        // ── Scoring token — explicit pixel positions for reliability ───────
        float cTop = -44f;
        float cH   = 100f;
        float cW   = -M * 2f; // full width minus margins

        // 1. Thick drop shadow (largest, furthest back)
        MakeRoundedLayer(section.transform, "ScoreShadow",
            new Vector2(M+5f, cTop-7f), new Vector2(cW-5f, cH),
            new Color(0f,0f,0f,0.85f), 12);

        // 2. Outer border/bevel (slightly smaller than shadow)
        MakeRoundedLayer(section.transform, "ScoreOuterBorder",
            new Vector2(M, cTop-4f), new Vector2(cW, cH),
            new Color(0.02f,0.01f,0.00f,1f), 12);

        // 3. Bright top edge of outer border (catches light)
        MakeRoundedLayer(section.transform, "ScoreTopEdge",
            new Vector2(M+1f, cTop), new Vector2(cW-2f, 3f),
            new Color(1f,1f,1f,0.18f), 4);

        // 4. Main container (the "token" surface — sits on top of bevel)
        var bgGO = MakeRoundedLayer(section.transform, "ScoreContainer",
            new Vector2(M+1f, cTop-1f), new Vector2(cW-2f, cH-3f),
            new Color(0.10f,0.08f,0.06f,1f), 11);

        // ── Three cells inside bgGO ────────────────────────────────────────
        // Cells use explicit pixel sizes so rounding always works
        float cellH = cH - 3f - 8f; // container height minus padding
        float cellY = -4f;           // from top of container

        // We'll lay out cells manually as fixed-width children
        // Use a horizontal layout approach with explicit positions
        BuildCell3D("PointsPill", "PreviewBase", bgGO.transform,
            new Vector2(0f,0f), new Vector2(0.30f,1f), cellY, cellH,
            "POINTS", new Color(0.08f,0.18f,0.35f,1f),
            new Color(0.78f,0.90f,1.00f,1f),
            new Color(0.55f,0.72f,0.95f,0.7f));

        // × separator
        var xGO  = R("Times", bgGO.transform,
            new Vector2(0.30f,0f), new Vector2(0.38f,1f),
            Vector2.zero, Vector2.zero);
        var xTMP = xGO.AddComponent<TextMeshProUGUI>();
        xTMP.text = "×"; xTMP.fontSize = 36f;
        xTMP.alignment = TextAlignmentOptions.Center;
        xTMP.fontStyle = FontStyles.Bold;
        xTMP.color = new Color(1f,0.85f,0.40f,1f);
        xTMP.outlineWidth = 0.3f;
        xTMP.outlineColor = new Color32(60,40,0,255);
        if (headerFont) xTMP.font = headerFont;

        BuildCell3D("MultPill", "PreviewMult", bgGO.transform,
            new Vector2(0.38f,0f), new Vector2(0.62f,1f), cellY, cellH,
            "MULT", new Color(0.28f,0.08f,0.04f,1f),
            new Color(1.00f,0.74f,0.46f,1f),
            new Color(0.95f,0.45f,0.20f,0.7f));

        // = separator
        var eqGO  = R("Equals", bgGO.transform,
            new Vector2(0.62f,0f), new Vector2(0.70f,1f),
            Vector2.zero, Vector2.zero);
        var eqTMP = eqGO.AddComponent<TextMeshProUGUI>();
        eqTMP.text = "="; eqTMP.fontSize = 36f;
        eqTMP.alignment = TextAlignmentOptions.Center;
        eqTMP.fontStyle = FontStyles.Bold;
        eqTMP.color = new Color(1f,0.85f,0.40f,1f);
        eqTMP.outlineWidth = 0.3f;
        eqTMP.outlineColor = new Color32(60,40,0,255);
        if (headerFont) eqTMP.font = headerFont;

        BuildCell3D("ScorePill", "PreviewFinal", bgGO.transform,
            new Vector2(0.70f,0f), new Vector2(1.00f,1f), cellY, cellH,
            "SCORE", new Color(0.05f,0.18f,0.08f,1f),
            new Color(0.25f,0.92f,0.42f,1f),
            new Color(0.22f,0.65f,0.35f,0.7f));

        // ── Tier bar ──────────────────────────────────────────────────────
        float tTop = cTop - cH - 12f;
        float tH   = 32f;

        var ttRow = R("TierTextRow", section.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, tTop+4f), new Vector2(0f,20f));
        ttRow.GetComponent<RectTransform>().pivot = new Vector2(0f,0f);

        var tierGO  = R("PreviewTier", ttRow.transform,
            new Vector2(0f,0f), new Vector2(0.55f,1f),
            new Vector2(8f,0f), Vector2.zero);
        var tierTMP = tierGO.AddComponent<TextMeshProUGUI>();
        tierTMP.text = ""; tierTMP.fontSize = 16f;
        tierTMP.alignment = TextAlignmentOptions.Left;
        tierTMP.fontStyle = FontStyles.Bold;
        tierTMP.color = new Color(c_gold.r,c_gold.g,c_gold.b,1f);
        tierTMP.raycastTarget = false;
        if (bodyFont) tierTMP.font = bodyFont;

        var nextGO  = R("PreviewNextHint", ttRow.transform,
            new Vector2(0.55f,0f), new Vector2(1f,1f),
            new Vector2(0f,0f), new Vector2(-8f,0f));
        var nextTMP = nextGO.AddComponent<TextMeshProUGUI>();
        nextTMP.text = ""; nextTMP.fontSize = 11f;
        nextTMP.alignment = TextAlignmentOptions.Right;
        nextTMP.color = new Color(c_gold.r,c_gold.g,c_gold.b,0.65f);
        nextTMP.raycastTarget = false;
        if (bodyFont) nextTMP.font = bodyFont;

        var tierRow = R("TierRow", section.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(M, tTop), new Vector2(-M*2f, tH));
        tierRow.GetComponent<RectTransform>().pivot = new Vector2(0f,1f);
        tierRow.AddComponent<Image>().color = new Color(0.06f,0.05f,0.03f,1f);
        var tbc = tierRow.AddComponent<TierBarController>();
        tbc.audioSource  = tierRow.AddComponent<AudioSource>();
        tbc.tierDotsText = tierTMP;
        tbc.nextHintText = nextTMP;

        // ── Breakdown ──────────────────────────────────────────────────────
        float bTop = tTop - tH - 20f;
        float bH   = 108f;

        var blGO = R("BreakLabel", section.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(8f, bTop+2f), new Vector2(-8f,16f));
        blGO.GetComponent<RectTransform>().pivot = new Vector2(0f,0f);
        var blTMP = blGO.AddComponent<TextMeshProUGUI>();
        blTMP.text = "SCORING BREAKDOWN"; blTMP.fontSize = 8f;
        blTMP.alignment = TextAlignmentOptions.Left;
        blTMP.color = new Color(c_parchDim.r,c_parchDim.g,c_parchDim.b,0.45f);
        blTMP.fontStyle = FontStyles.SmallCaps; blTMP.raycastTarget = false;
        if (bodyFont) blTMP.font = bodyFont;

        var bp = R("BreakdownPanel", section.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f, bTop-18f), new Vector2(0f, bH));
        bp.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
        bp.AddComponent<Image>().color = new Color(0.05f,0.04f,0.03f,0.9f);

        R("BreakDiv", bp.transform,
            new Vector2(0.05f,1f), new Vector2(0.95f,1f),
            Vector2.zero, new Vector2(0f,1f))
            .AddComponent<Image>().color = new Color(1f,1f,1f,0.06f);

        for (int i = 0; i < 3; i++)
        {
            float ry = -8f - i * 33f;
            var sg = R($"BreakSrc{i}", bp.transform,
                new Vector2(0f,1f), new Vector2(0.68f,1f),
                new Vector2(8f,ry), new Vector2(-4f,28f));
            sg.GetComponent<RectTransform>().pivot = new Vector2(0f,1f);
            var st = sg.AddComponent<TextMeshProUGUI>();
            st.text = ""; st.fontSize = 13f;
            st.alignment = TextAlignmentOptions.Left;
            st.color = new Color(c_parchDim.r,c_parchDim.g,c_parchDim.b,0.75f);
            st.raycastTarget = false;
            if (bodyFont) st.font = bodyFont;

            var vg = R($"BreakVal{i}", bp.transform,
                new Vector2(0.68f,1f), new Vector2(1f,1f),
                new Vector2(0f,ry), new Vector2(-8f,28f));
            vg.GetComponent<RectTransform>().pivot = new Vector2(1f,1f);
            var vt = vg.AddComponent<TextMeshProUGUI>();
            vt.text = ""; vt.fontSize = 13f;
            vt.fontStyle = FontStyles.Bold;
            vt.alignment = TextAlignmentOptions.Right;
            vt.color = new Color(1.00f,0.74f,0.46f,0.9f);
            vt.raycastTarget = false;
            if (headerFont) vt.font = headerFont;
        }

        return hdrTMP;
    }

    // Creates a plain colored box at explicit position+size (top-left pivot, top anchor)
    // No rounded sprite — just solid color, reliable always
    GameObject MakeRoundedLayer(Transform parent, string name,
        Vector2 pos, Vector2 size, Color color, int radius)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var img = go.AddComponent<Image>();
        img.color  = color;
        img.raycastTarget = false;
        return go;
    }

    // Builds a 3D scoring cell — stretch fills its anchor slice
    void BuildCell3D(string pillName, string valueName, Transform parent,
        Vector2 aMin, Vector2 aMax, float yOffset, float height,
        string label, Color bg, Color valCol, Color lblCol)
    {
        // Root — stretch anchors, small inset
        var cell = R(pillName, parent, aMin, aMax,
            new Vector2(2f, 2f), new Vector2(-2f, -2f));

        // Solid background with animated swirl shader
        var bgImg = cell.AddComponent<Image>();
        bgImg.color = bg;

        // Apply swirl shader directly — no component needed
        var swirlShader = Shader.Find("Custom/ScoringCellSwirl");
        if (swirlShader != null)
        {
            var mat = new Material(swirlShader);
            mat.SetColor("_Color",  bg);
            mat.SetColor("_Color2", new Color(
                Mathf.Clamp01(bg.r + 0.12f),
                Mathf.Clamp01(bg.g + 0.12f),
                Mathf.Clamp01(bg.b + 0.12f), 1f));
            mat.SetFloat("_Speed",     0.3f);
            mat.SetFloat("_Scale",     3.5f);
            mat.SetFloat("_Intensity", 0.15f);
            bgImg.material = mat;
        }

        // Top highlight strip
        var hi   = R("Hi", cell.transform,
            new Vector2(0f,0.6f), new Vector2(1f,1f),
            Vector2.zero, Vector2.zero);
        var hiImg = hi.AddComponent<Image>();
        hiImg.color = new Color(1f,1f,1f,0.08f);
        hiImg.raycastTarget = false;

        // Bottom shadow strip
        var sh   = R("Sh", cell.transform,
            new Vector2(0f,0f), new Vector2(1f,0.25f),
            Vector2.zero, Vector2.zero);
        var shImg = sh.AddComponent<Image>();
        shImg.color = new Color(0f,0f,0f,0.20f);
        shImg.raycastTarget = false;

        // Label
        var lg  = R("Lbl", cell.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f,-5f), new Vector2(0f,14f));
        lg.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
        var lt  = lg.AddComponent<TextMeshProUGUI>();
        lt.text = label; lt.fontSize = 9f;
        lt.alignment = TextAlignmentOptions.Center;
        lt.color = lblCol;
        lt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        lt.outlineWidth = 0.2f; lt.outlineColor = new Color(0f,0f,0f,1f);
        lt.raycastTarget = false;
        if (bodyFont) lt.font = bodyFont;

        // Value shadow
        var vsh  = R("ValSh", cell.transform,
            Vector2.zero, Vector2.one,
            new Vector2(1f,4f), new Vector2(2f,-16f));
        var vsht = vsh.AddComponent<TextMeshProUGUI>();
        vsht.text = "—"; vsht.fontSize = 32f;
        vsht.alignment = TextAlignmentOptions.Center;
        vsht.color = new Color(0f,0f,0f,0.45f);
        vsht.fontStyle = FontStyles.Bold;
        vsht.raycastTarget = false;
        if (headerFont) vsht.font = headerFont;

        // Value
        var vg  = R(valueName, cell.transform,
            Vector2.zero, Vector2.one,
            new Vector2(0f,5f), new Vector2(0f,-16f));
        var vt  = vg.AddComponent<TextMeshProUGUI>();
        vt.text = "—"; vt.fontSize = 32f;
        vt.alignment = TextAlignmentOptions.Center;
        vt.color = valCol;
        vt.fontStyle = FontStyles.Bold;
        vt.outlineWidth = 0.1f; vt.outlineColor = new Color(0f,0f,0f,0.5f);
        vt.raycastTarget = false;
        if (headerFont) vt.font = headerFont;
    }


    // =========================================================================
    // PLAY BUTTON
    // =========================================================================
    Button BuildPlayButton()
    {
        var btn = R("PlayButton", transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 90f), new Vector2(260f, 58f));
        btn.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        var img = btn.AddComponent<Image>(); img.color = c_greenBtn;
        var b   = btn.AddComponent<Button>(); b.targetGraphic = img;
        var bc  = b.colors;
        bc.normalColor      = c_greenBtn;
        bc.highlightedColor = c_greenBtnHi;
        bc.pressedColor     = new Color(0.08f, 0.20f, 0.05f);
        b.colors = bc;
        b.onClick.AddListener(() =>
        {
            AudioManager.Instance?.Play("card_play");
            GameManager.Instance?.OnPlayPressed();
        });

        R("Top", btn.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            Vector2.zero, new Vector2(0f, 2f))
            .AddComponent<Image>().color = new Color(c_blood.r,c_blood.g,c_blood.b,0.45f);

        var lGO = R("Lbl", btn.transform,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var lt = lGO.AddComponent<TextMeshProUGUI>();
        lt.text = "OFFER"; lt.fontSize = 22f;
        lt.alignment = TextAlignmentOptions.Center;
        lt.color = c_parchLight; lt.fontStyle = FontStyles.Bold;
        if (headerFont) lt.font = headerFont;

        return b;
    }

    // =========================================================================
    // JOURNAL BUTTON
    // =========================================================================
    void BuildJournalButton()
    {
        var btn = R("JournalButton", transform,
            new Vector2(1f,1f), new Vector2(1f,1f),
            new Vector2(-16f,-16f), new Vector2(130f,36f));
        btn.GetComponent<RectTransform>().pivot = new Vector2(1f,1f);
        var img = btn.AddComponent<Image>();
        img.color = new Color(c_ironDark.r,c_ironDark.g,c_ironDark.b,0.85f);
        var b = btn.AddComponent<Button>(); b.targetGraphic = img;
        b.onClick.AddListener(() => SettingsUI.Instance?.Open());
        var lGO = R("Lbl",btn.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var lt  = lGO.AddComponent<TextMeshProUGUI>();
        lt.text = "Journal"; lt.fontSize = 12f;
        lt.alignment = TextAlignmentOptions.Center; lt.color = c_parchDim;
        if (bodyFont) lt.font = bodyFont;
    }

    // =========================================================================
    // SHOP PANEL
    // =========================================================================
    GameObject BuildShopPanel(
        out TextMeshProUGUI relicName,  out TextMeshProUGUI relicDesc,  out TextMeshProUGUI relicCost,
        out TextMeshProUGUI cardName,   out TextMeshProUGUI cardRarity, out TextMeshProUGUI cardCost,
        out TextMeshProUGUI consumName, out TextMeshProUGUI consumDesc, out TextMeshProUGUI consumCost,
        out TextMeshProUGUI burnCost,
        out Button buyRelic, out Button buyCard, out Button buyConsumable,
        out Button burnBtn,  out Button leaveBtn)
    {
        var panel = R("ShopPanel",transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        panel.AddComponent<Image>().color = new Color(0.04f,0.03f,0.02f,0.97f);
        panel.SetActive(false);

        var hdr = R("ShopHeader",panel.transform,
            new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(0f,-44f),new Vector2(0f,80f));
        hdr.AddComponent<Image>().color = new Color(0.08f,0.06f,0.03f,1f);
        R("HA",hdr.transform,new Vector2(0f,1f),new Vector2(1f,1f),Vector2.zero,new Vector2(0f,3f)).AddComponent<Image>().color=c_blood;
        R("HB",hdr.transform,new Vector2(0f,0f),new Vector2(1f,0f),Vector2.zero,new Vector2(0f,1f)).AddComponent<Image>().color=new Color(c_rust.r,c_rust.g,c_rust.b,0.5f);

        var stGO=R("ST",hdr.transform,new Vector2(0f,0f),new Vector2(1f,1f),new Vector2(0f,10f),Vector2.zero);
        var st=stGO.AddComponent<TextMeshProUGUI>();
        st.text="THE MARKET"; st.fontSize=32f; st.alignment=TextAlignmentOptions.Center;
        st.color=c_gold; st.fontStyle=FontStyles.Bold|FontStyles.SmallCaps;
        if(headerFont)st.font=headerFont;

        var gdGO=R("GoldDisp",hdr.transform,new Vector2(1f,0.5f),new Vector2(1f,0.5f),new Vector2(-24f,8f),new Vector2(140f,32f));
        gdGO.GetComponent<RectTransform>().pivot=new Vector2(1f,0.5f);
        gdGO.AddComponent<Image>().color=new Color(c_gold.r,c_gold.g,c_gold.b,0.12f);
        var gdTxt=R("GDTxt",gdGO.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var gd=gdTxt.AddComponent<TextMeshProUGUI>();
        gd.text="5g"; gd.fontSize=16f; gd.alignment=TextAlignmentOptions.Center;
        gd.color=c_gold; gd.fontStyle=FontStyles.Bold;
        if(headerFont)gd.font=headerFont;

        var rs =ShopSlot("RelicSlot",  panel.transform,new Vector2(-320f,-10f),"RELIC",c_gold);
        var cs2=ShopSlot("CardSlot",   panel.transform,new Vector2(   0f,-10f),"CARD MOD",c_blood);
        var os =ShopSlot("ConsumSlot", panel.transform,new Vector2( 320f,-10f),"CONSUMABLE",new Color(0.85f,0.55f,0.15f));

        relicName =rs.Find("Title")?.GetComponent<TextMeshProUGUI>();
        relicDesc =rs.Find("Desc") ?.GetComponent<TextMeshProUGUI>();
        relicCost =rs.Find("Cost") ?.GetComponent<TextMeshProUGUI>();
        buyRelic  =rs.Find("BuyBtn")?.GetComponent<Button>();
        cardName  =cs2.Find("Title")?.GetComponent<TextMeshProUGUI>();
        cardRarity=cs2.Find("Desc") ?.GetComponent<TextMeshProUGUI>();
        cardCost  =cs2.Find("Cost") ?.GetComponent<TextMeshProUGUI>();
        buyCard   =cs2.Find("BuyBtn")?.GetComponent<Button>();
        consumName=os.Find("Title")?.GetComponent<TextMeshProUGUI>();
        consumDesc=os.Find("Desc") ?.GetComponent<TextMeshProUGUI>();
        consumCost=os.Find("Cost") ?.GetComponent<TextMeshProUGUI>();
        buyConsumable=os.Find("BuyBtn")?.GetComponent<Button>();

        var bs=ShopSlot("BurnSlot",panel.transform,new Vector2(0f,-272f),"BURN CARD",c_blood);
        bs.Find("Title")?.GetComponent<TextMeshProUGUI>()?.SetText("BURN A CARD");
        var bDesc=bs.Find("Desc")?.GetComponent<TextMeshProUGUI>();
        if(bDesc)bDesc.text="Permanently remove a selected card from your deck.";
        burnCost=bs.Find("Cost")  ?.GetComponent<TextMeshProUGUI>();
        burnBtn =bs.Find("BuyBtn")?.GetComponent<Button>();
        if(burnBtn!=null){
            var bi=burnBtn.GetComponent<Image>(); if(bi)bi.color=new Color(0.40f,0.07f,0.06f);
            var rbc=burnBtn.colors; rbc.normalColor=new Color(0.40f,0.07f,0.06f);
            rbc.highlightedColor=new Color(0.58f,0.12f,0.10f); burnBtn.colors=rbc;
        }

        var lo=R("LeaveBtn",panel.transform,new Vector2(0.5f,0f),new Vector2(0.5f,0f),new Vector2(0f,40f),new Vector2(240f,54f));
        var li=lo.AddComponent<Image>(); li.color=new Color(0.36f,0.07f,0.06f);
        leaveBtn=lo.AddComponent<Button>(); leaveBtn.targetGraphic=li;
        var lc=leaveBtn.colors; lc.normalColor=new Color(0.36f,0.07f,0.06f); lc.highlightedColor=new Color(0.52f,0.11f,0.09f); leaveBtn.colors=lc;
        leaveBtn.onClick.AddListener(()=>AudioManager.Instance?.Play("ui_click"));
        var llGO=R("Lbl",lo.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var ll=llGO.AddComponent<TextMeshProUGUI>();
        ll.text="LEAVE MARKET"; ll.fontSize=20f; ll.alignment=TextAlignmentOptions.Center;
        ll.color=c_parchLight; ll.fontStyle=FontStyles.Bold;
        if(headerFont)ll.font=headerFont;

        return panel;
    }

    Transform ShopSlot(string name,Transform parent,Vector2 pos,string typeLabel,Color accent)
    {
        const int SW=280,SH=330;
        var slot=R(name,parent,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),pos,new Vector2(SW,SH));
        slot.AddComponent<Image>().color=new Color(0.13f,0.11f,0.08f,1f);
        R("Frame",slot.transform,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(4f,4f)).AddComponent<Image>().color=new Color(accent.r,accent.g,accent.b,0.22f);
        R("TopBar",slot.transform,new Vector2(0f,1f),new Vector2(1f,1f),Vector2.zero,new Vector2(0f,4f)).AddComponent<Image>().color=accent;
        var tlGO=R("TypeLabel",slot.transform,new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(0f,-16f),new Vector2(-12f,22f));
        var tl=tlGO.AddComponent<TextMeshProUGUI>(); tl.text=typeLabel; tl.fontSize=9f;
        tl.alignment=TextAlignmentOptions.Center; tl.color=new Color(accent.r*1.2f,accent.g*1.2f,accent.b*0.8f,1f);
        tl.fontStyle=FontStyles.Bold|FontStyles.SmallCaps; tl.raycastTarget=false; if(headerFont)tl.font=headerFont;
        R("TLDiv",slot.transform,new Vector2(0.1f,1f),new Vector2(0.9f,1f),new Vector2(0f,-30f),new Vector2(0f,1f)).AddComponent<Image>().color=new Color(accent.r,accent.g,accent.b,0.3f);
        var tiGO=R("Title",slot.transform,new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(0f,-60f),new Vector2(-16f,52f));
        var ti=tiGO.AddComponent<TextMeshProUGUI>(); ti.text="???"; ti.fontSize=17f;
        ti.alignment=TextAlignmentOptions.Center; ti.color=c_parchLight; ti.fontStyle=FontStyles.Bold;
        ti.textWrappingMode=TextWrappingModes.Normal; ti.raycastTarget=false; if(headerFont)ti.font=headerFont;
        var deGO=R("Desc",slot.transform,new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(0f,-126f),new Vector2(-24f,100f));
        var de=deGO.AddComponent<TextMeshProUGUI>(); de.text=""; de.fontSize=11.5f;
        de.alignment=TextAlignmentOptions.Center; de.color=new Color(0.80f,0.72f,0.55f,0.92f);
        de.textWrappingMode=TextWrappingModes.Normal; de.raycastTarget=false; if(bodyFont)de.font=bodyFont;
        R("CostDiv",slot.transform,new Vector2(0.1f,0f),new Vector2(0.9f,0f),new Vector2(0f,76f),new Vector2(0f,1f)).AddComponent<Image>().color=new Color(accent.r,accent.g,accent.b,0.25f);
        var coGO=R("Cost",slot.transform,new Vector2(0f,0f),new Vector2(1f,0f),new Vector2(0f,60f),new Vector2(-16f,30f));
        var co=coGO.AddComponent<TextMeshProUGUI>(); co.text="0g"; co.fontSize=16f;
        co.alignment=TextAlignmentOptions.Center; co.color=c_gold; co.fontStyle=FontStyles.Bold; co.raycastTarget=false; if(headerFont)co.font=headerFont;
        var boGO=R("BuyBtn",slot.transform,new Vector2(0.5f,0f),new Vector2(0.5f,0f),new Vector2(0f,24f),new Vector2(200f,40f));
        var bi=boGO.AddComponent<Image>(); bi.color=c_greenBtn;
        var bu=boGO.AddComponent<Button>(); bu.targetGraphic=bi;
        var bc=bu.colors; bc.normalColor=c_greenBtn; bc.highlightedColor=c_greenBtnHi; bc.pressedColor=new Color(0.08f,0.20f,0.05f); bu.colors=bc;
        bu.onClick.AddListener(()=>AudioManager.Instance?.Play("ui_click"));
        var blGO=R("BtnLbl",boGO.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        var bl=blGO.AddComponent<TextMeshProUGUI>(); bl.text="BUY"; bl.fontSize=15f;
        bl.alignment=TextAlignmentOptions.Center; bl.color=c_parchLight; bl.fontStyle=FontStyles.Bold; bl.raycastTarget=false; if(headerFont)bl.font=headerFont;
        return slot.transform;
    }

    // =========================================================================
    // GAME OVER
    // =========================================================================
    GameObject BuildGameOverPanel(out TextMeshProUGUI scoreText)
    {
        var panel=R("GameOverPanel",transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
        panel.AddComponent<Image>().color=new Color(0f,0f,0f,0.94f); panel.SetActive(false);
        var tGO=R("Title",panel.transform,new Vector2(0f,0.5f),new Vector2(1f,0.5f),new Vector2(0f,70f),new Vector2(0f,80f));
        var tTMP=tGO.AddComponent<TextMeshProUGUI>(); tTMP.text="* CONDEMNED *"; tTMP.fontSize=58f;
        tTMP.alignment=TextAlignmentOptions.Center; tTMP.color=c_blood; tTMP.fontStyle=FontStyles.Bold; if(headerFont)tTMP.font=headerFont;
        var sGO=R("Sub",panel.transform,new Vector2(0f,0.5f),new Vector2(1f,0.5f),new Vector2(0f,14f),new Vector2(0f,40f));
        var sTMP=sGO.AddComponent<TextMeshProUGUI>(); sTMP.text="Your soul is forfeit."; sTMP.fontSize=20f;
        sTMP.alignment=TextAlignmentOptions.Center; sTMP.color=c_parchDim; sTMP.fontStyle=FontStyles.Italic; if(bodyFont)sTMP.font=bodyFont;
        var scGO=R("Score",panel.transform,new Vector2(0f,0.5f),new Vector2(1f,0.5f),new Vector2(0f,-32f),new Vector2(0f,50f));
        scoreText=scGO.AddComponent<TextMeshProUGUI>(); scoreText.text="Final Score: 0"; scoreText.fontSize=28f;
        scoreText.alignment=TextAlignmentOptions.Center; scoreText.color=c_parch; if(headerFont)scoreText.font=headerFont;
        return panel;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================
    GameObject R(string name, Transform parent,
        Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var o  = new GameObject(name);
        o.transform.SetParent(parent, false);
        var rt = o.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return o;
    }

    TextMeshProUGUI TL(string name, Transform parent, string text, float size,
        Vector2 pos, Vector2 sz, Color col, TMP_FontAsset font)
    {
        var go = R(name, parent,
            new Vector2(0f,1f), new Vector2(1f,1f), pos, sz);
        go.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        var t  = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.alignment = TextAlignmentOptions.Left;
        t.color = col; t.raycastTarget = false;
        if (font) t.font = font;
        return t;
    }

    // Unified scoring section — one tinted cell inside the score row
    void ScoringSection(string pillName, string valueName, Transform parent,
        Vector2 aMin, Vector2 aMax,
        string label, Color bg, Color valCol, Color labelCol,
        bool roundLeft, bool roundRight)
    {
        var cell = R(pillName, parent, aMin, aMax, Vector2.zero, Vector2.zero);
        var cellImg = cell.AddComponent<Image>();

        // Rounded sprite only on outer ends
        if (roundLeft || roundRight)
        {
            cellImg.sprite = MakeRoundedSprite(128, 64, 14, Color.white);
            cellImg.type   = Image.Type.Sliced;
        }
        cellImg.color = bg;

        // Label — small, top center
        var lblGO = R("Lbl", cell.transform,
            new Vector2(0f,1f), new Vector2(1f,1f),
            new Vector2(0f,-4f), new Vector2(0f,16f));
        lblGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f,1f);
        var lt = lblGO.AddComponent<TextMeshProUGUI>();
        lt.text      = label;
        lt.fontSize  = 9f;
        lt.alignment = TextAlignmentOptions.Center;
        lt.color     = labelCol;
        lt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
        lt.raycastTarget = false;
        if (bodyFont) lt.font = bodyFont;

        // Value — large, fills cell
        var vGO = R(valueName, cell.transform,
            new Vector2(0f,0f), new Vector2(1f,1f),
            new Vector2(0f,4f), new Vector2(0f,-18f));
        var vt = vGO.AddComponent<TextMeshProUGUI>();
        vt.text      = "—";
        vt.fontSize  = 30f;
        vt.alignment = TextAlignmentOptions.Center;
        vt.color     = valCol;
        vt.fontStyle = FontStyles.Bold;
        vt.raycastTarget = false;
        if (headerFont) vt.font = headerFont;
    }

    // Creates a rounded rect sprite for pill-shaped UI elements
    static Sprite MakeRoundedSprite(int w, int h, int radius, Color col)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int cx = Mathf.Min(x, w - 1 - x);
                int cy = Mathf.Min(y, h - 1 - y);
                bool inside = true;
                if (cx < radius && cy < radius)
                {
                    float dx = radius - cx, dy = radius - cy;
                    inside = (dx * dx + dy * dy) <= (radius * radius);
                }
                px[y * w + x] = inside ? col : new Color(0, 0, 0, 0);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f,
            0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }
}