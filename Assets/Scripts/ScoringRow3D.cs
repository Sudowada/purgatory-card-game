using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds a single 3D-embossed scoring cell — thick bevel, chunky rounded corners,
/// strong drop shadow. Balatro-style physical token feel.
/// </summary>
public static class ScoringRow3D
{
    public static TextMeshProUGUI Build(
        string        pillName,
        string        valueName,
        Transform     parent,
        Vector2       anchorMin,
        Vector2       anchorMax,
        string        label,
        Color         bgColor,
        Color         valueColor,
        Color         labelColor,
        bool          roundLeft,
        bool          roundRight,
        TMP_FontAsset headerFont,
        TMP_FontAsset bodyFont)
    {
        int cornerR = 18;

        // ── Root ──────────────────────────────────────────────────────────────
        var cell   = new GameObject(pillName);
        cell.transform.SetParent(parent, false);
        var cellRT = cell.AddComponent<RectTransform>();
        cellRT.anchorMin = anchorMin;
        cellRT.anchorMax = anchorMax;
        cellRT.offsetMin = new Vector2( 2f,  2f);
        cellRT.offsetMax = new Vector2(-2f, -2f);

        // ── Layer 1: Hard drop shadow ─────────────────────────────────────────
        AddLayer("Shadow", cell.transform,
            new Vector2(5f, -7f), new Vector2(5f, -5f),
            new Color(0f, 0f, 0f, 0.7f),
            roundLeft, roundRight, cornerR);

        // ── Layer 2: Dark bevel bottom — creates thickness ────────────────────
        Color bevelDark = new Color(
            bgColor.r * 0.35f,
            bgColor.g * 0.35f,
            bgColor.b * 0.35f, 1f);
        AddLayer("BevelDark", cell.transform,
            new Vector2(0f, -6f), new Vector2(0f, 0f),
            bevelDark,
            roundLeft, roundRight, cornerR);

        // ── Layer 3: Mid bevel — one step lighter ─────────────────────────────
        Color bevelMid = new Color(
            bgColor.r * 0.55f,
            bgColor.g * 0.55f,
            bgColor.b * 0.55f, 1f);
        AddLayer("BevelMid", cell.transform,
            new Vector2(0f, -3f), new Vector2(0f, 0f),
            bevelMid,
            roundLeft, roundRight, cornerR);

        // ── Layer 4: Main surface ─────────────────────────────────────────────
        AddLayer("Surface", cell.transform,
            new Vector2(0f, 2f), new Vector2(0f, 0f),
            bgColor,
            roundLeft, roundRight, cornerR);

        // ── Layer 5: Top highlight — bright strip catching light ──────────────
        var hi   = MakeChild("Highlight", cell.transform);
        var hiRT = hi.GetComponent<RectTransform>();
        hiRT.anchorMin        = new Vector2(0.04f, 0.55f);
        hiRT.anchorMax        = new Vector2(0.96f, 1f);
        hiRT.offsetMin        = new Vector2(0f,  2f);
        hiRT.offsetMax        = new Vector2(0f, -4f);
        var hiImg             = hi.AddComponent<Image>();
        hiImg.color           = new Color(1f, 1f, 1f, 0.22f);
        hiImg.raycastTarget   = false;
        hiImg.sprite          = MakeRoundedSprite(64, 32, cornerR / 2, Color.white);
        hiImg.type            = Image.Type.Sliced;

        // ── Layer 6: Bottom inner shadow — depth at base ──────────────────────
        var ish   = MakeChild("InnerShadow", cell.transform);
        var ishRT = ish.GetComponent<RectTransform>();
        ishRT.anchorMin      = new Vector2(0.04f, 0f);
        ishRT.anchorMax      = new Vector2(0.96f, 0.3f);
        ishRT.offsetMin      = new Vector2(0f, 2f);
        ishRT.offsetMax      = Vector2.zero;
        var ishImg           = ish.AddComponent<Image>();
        ishImg.color         = new Color(0f, 0f, 0f, 0.25f);
        ishImg.raycastTarget = false;

        // ── Layer 7: Label ────────────────────────────────────────────────────
        var lblGO   = MakeChild("Lbl", cell.transform);
        var lblRT   = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin        = new Vector2(0f, 1f);
        lblRT.anchorMax        = new Vector2(1f, 1f);
        lblRT.pivot            = new Vector2(0.5f, 1f);
        lblRT.anchoredPosition = new Vector2(0f, -5f);
        lblRT.sizeDelta        = new Vector2(0f, 16f);
        var lt = lblGO.AddComponent<TextMeshProUGUI>();
        lt.text           = label;
        lt.fontSize       = 9f;
        lt.alignment      = TextAlignmentOptions.Center;
        lt.color          = labelColor;
        lt.fontStyle      = FontStyles.Bold | FontStyles.SmallCaps;
        lt.raycastTarget  = false;
        lt.outlineWidth   = 0.2f;
        lt.outlineColor   = new Color(0f, 0f, 0f, 1f);
        if (bodyFont) lt.font = bodyFont;

        // ── Layer 8: Value shadow (emboss offset) ─────────────────────────────
        var vshGO   = MakeChild("ValShadow", cell.transform);
        var vshRT   = vshGO.GetComponent<RectTransform>();
        vshRT.anchorMin = Vector2.zero;
        vshRT.anchorMax = Vector2.one;
        vshRT.offsetMin = new Vector2( 2f,  5f);
        vshRT.offsetMax = new Vector2( 3f, -18f);
        var vshTMP      = vshGO.AddComponent<TextMeshProUGUI>();
        vshTMP.text          = "—";
        vshTMP.fontSize      = 32f;
        vshTMP.alignment     = TextAlignmentOptions.Center;
        vshTMP.color         = new Color(0f, 0f, 0f, 0.45f);
        vshTMP.fontStyle     = FontStyles.Bold;
        vshTMP.raycastTarget = false;
        if (headerFont) vshTMP.font = headerFont;

        // ── Layer 9: Value text ───────────────────────────────────────────────
        var vGO   = MakeChild(valueName, cell.transform);
        var vRT   = vGO.GetComponent<RectTransform>();
        vRT.anchorMin = Vector2.zero;
        vRT.anchorMax = Vector2.one;
        vRT.offsetMin = new Vector2(0f,  6f);
        vRT.offsetMax = new Vector2(0f, -18f);
        var vt    = vGO.AddComponent<TextMeshProUGUI>();
        vt.text          = "—";
        vt.fontSize      = 32f;
        vt.alignment     = TextAlignmentOptions.Center;
        vt.color         = valueColor;
        vt.fontStyle     = FontStyles.Bold;
        vt.raycastTarget = false;
        vt.outlineWidth  = 0.12f;
        vt.outlineColor  = new Color(0f, 0f, 0f, 0.6f);
        if (headerFont) vt.font = headerFont;

        return vt;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void AddLayer(string name, Transform parent,
        Vector2 offsetMin, Vector2 offsetMax,
        Color color, bool roundLeft, bool roundRight, int cornerR)
    {
        var go   = MakeChild(name, parent);
        var rt   = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        var img  = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        // Always round — outer pills get full corner, inner gets smaller
        img.sprite = MakeRoundedSprite(128, 64, cornerR, Color.white);
        img.type   = Image.Type.Sliced;
    }

    static GameObject MakeChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    public static Sprite MakeRoundedSprite(int w, int h, int radius, Color col)
    {
        radius = Mathf.Min(radius, w / 2, h / 2);
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
                    inside = (dx * dx + dy * dy) <= (float)(radius * radius);
                }
                px[y * w + x] = inside ? col : new Color(0f, 0f, 0f, 0f);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }
}