using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies a wood texture to a UI Image with a dark vignette border
/// to create the wooden panel look from the reference image.
/// 
/// HOW TO USE:
/// 1. Add this component to your LeftCol or HUD panel GameObject
/// 2. Drag your wood texture into the "Wood Texture" slot
/// 3. Adjust the settings to taste
/// 
/// The script creates:
///   - Wood texture background
///   - Dark vignette around edges (makes it look like a recessed panel)
///   - Optional carved border lines
///   - Dark overlay to keep text readable
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WoodPanelBackground : MonoBehaviour
{
    [Header("Texture")]
    [Tooltip("Drag your wood texture here (PNG/JPG)")]
    public Texture2D woodTexture;

    [Header("Appearance")]
    [Range(0f, 1f)] public float darkOverlay    = 0.45f; // how dark to tint the wood
    [Range(0f, 1f)] public float vignetteStrength= 0.70f; // edge darkness
    public Color                 tintColor       = new Color(0.55f, 0.35f, 0.15f); // warm brown tint
    public bool                  showBorder      = true;
    public Color                 borderColor     = new Color(0.25f, 0.12f, 0.04f);
    public float                 borderWidth     = 4f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private GameObject _woodGO;
    private GameObject _vignetteGO;
    private GameObject _overlayGO;
    private GameObject _borderGO;

    void Start() => Build();

    void Build()
    {
        var rt = GetComponent<RectTransform>();

        // Disable the existing Image component so wood shows through
        var existingImg = GetComponent<Image>();
        if (existingImg != null) existingImg.color = new Color(0,0,0,0);

        // 1. Wood texture layer
        _woodGO = MakeLayer("WoodBG", 0);
        var woodImg   = _woodGO.AddComponent<RawImage>();

        // Use provided texture or generate procedurally
        Texture2D tex = woodTexture != null
            ? woodTexture
            : WoodTextureGenerator.Generate(256, 512, Random.Range(0, 9999));

        woodImg.texture = tex;
        woodImg.uvRect  = new Rect(0, 0, 1, 2); // slight tile for tall panels
        woodImg.color   = tintColor;

        // 2. Dark overlay to keep text readable
        _overlayGO = MakeLayer("WoodOverlay", 1);
        var overlayImg = _overlayGO.AddComponent<Image>();
        overlayImg.color        = new Color(0f, 0f, 0f, darkOverlay);
        overlayImg.raycastTarget = false;

        // 3. Vignette — dark edges, lighter center
        _vignetteGO = MakeLayer("WoodVignette", 2);
        var vigImg = _vignetteGO.AddComponent<Image>();
        vigImg.color        = new Color(0f, 0f, 0f, 0f); // driven by shader below
        vigImg.raycastTarget = false;
        // Apply vignette using a radial gradient sprite
        vigImg.sprite = BuildVignetteSprite(64, 128, vignetteStrength);
        vigImg.type   = Image.Type.Simple;
        vigImg.preserveAspect = false;

        // 4. Border frame
        if (showBorder)
        {
            _borderGO = MakeLayer("WoodBorder", 3);
            var bImg = _borderGO.AddComponent<Image>();
            bImg.color        = new Color(0f,0f,0f,0f);
            bImg.raycastTarget = false;

            // Four border strips
            MakeBorderStrip("BTop",    _borderGO.transform, true,  true,  borderWidth, borderColor);
            MakeBorderStrip("BBottom", _borderGO.transform, true,  false, borderWidth, borderColor);
            MakeBorderStrip("BLeft",   _borderGO.transform, false, true,  borderWidth, borderColor);
            MakeBorderStrip("BRight",  _borderGO.transform, false, false, borderWidth, borderColor);

            // Inner highlight line (light edge — simulates raised lip)
            MakeBorderStrip("HTop",    _borderGO.transform, true,  true,  1f,
                new Color(1f, 0.85f, 0.55f, 0.25f), 3f);
            MakeBorderStrip("HLeft",   _borderGO.transform, false, true,  1f,
                new Color(1f, 0.85f, 0.55f, 0.15f), 3f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    GameObject MakeLayer(string name, int siblingIndex)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(siblingIndex);
        var rt      = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    void MakeBorderStrip(string name, Transform parent,
        bool horizontal, bool firstEdge, float thickness, Color color,
        float inset = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();

        if (horizontal)
        {
            rt.anchorMin = firstEdge
                ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rt.anchorMax = firstEdge
                ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rt.pivot        = new Vector2(0.5f, firstEdge ? 1f : 0f);
            rt.anchoredPosition = new Vector2(0f, firstEdge ? -inset : inset);
            rt.sizeDelta    = new Vector2(0f, thickness);
        }
        else
        {
            rt.anchorMin = firstEdge
                ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
            rt.anchorMax = firstEdge
                ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            rt.pivot        = new Vector2(firstEdge ? 0f : 1f, 0.5f);
            rt.anchoredPosition = new Vector2(firstEdge ? inset : -inset, 0f);
            rt.sizeDelta    = new Vector2(thickness, 0f);
        }

        var img         = go.AddComponent<Image>();
        img.color       = color;
        img.raycastTarget = false;
    }

    // Builds a vignette sprite — dark edges, transparent center
    static Sprite BuildVignetteSprite(int w, int h, float strength)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)(w - 1)) * 2f - 1f;
                float ny = (y / (float)(h - 1)) * 2f - 1f;

                // Smooth vignette — stronger at corners
                float dist  = Mathf.Sqrt(nx * nx + ny * ny) / Mathf.Sqrt(2f);
                float alpha = Mathf.Pow(Mathf.Clamp01(dist), 1.5f) * strength;

                px[y * w + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call to swap the wood texture at runtime.</summary>
    public void SetTexture(Texture2D tex)
    {
        woodTexture = tex;
        if (_woodGO != null)
        {
            var ri = _woodGO.GetComponent<RawImage>();
            if (ri != null) ri.texture = tex;
        }
    }
}