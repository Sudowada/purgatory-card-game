using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates a rounded-corner UI box using a properly configured
/// 9-slice sprite generated at runtime. No external assets needed.
/// 
/// Usage:
///   var box = RoundedBox.Make(parent, anchorMin, anchorMax, offsetMin, offsetMax, color, radius);
/// </summary>
public static class RoundedBox
{
    // Cache sprites by radius so we don't regenerate every frame
    private static System.Collections.Generic.Dictionary<int, Sprite> _cache
        = new System.Collections.Generic.Dictionary<int, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => _cache.Clear();

    /// <summary>
    /// Creates a rounded rect Image on a new child GameObject.
    /// Returns the Image component so you can set color, etc.
    /// </summary>
    public static Image Make(
        Transform parent,
        string    name,
        Vector2   anchorMin,
        Vector2   anchorMax,
        Vector2   offsetMin,
        Vector2   offsetMax,
        Color     color,
        int       radius = 12)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = anchorMin;
        rt.anchorMax    = anchorMax;
        rt.offsetMin    = offsetMin;
        rt.offsetMax    = offsetMax;

        var img         = go.AddComponent<Image>();
        img.sprite      = GetSprite(radius);
        img.type        = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 100f; // compensate for pixelsPerUnit=1
        img.color       = color;

        return img;
    }

    /// <summary>
    /// Same as Make but fills entire parent (anchor 0-1, zero offsets).
    /// </summary>
    public static Image Fill(Transform parent, string name, Color color, int radius = 12)
        => Make(parent, name,
            Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero,
            color, radius);

    // ── Sprite generation ─────────────────────────────────────────────────────

    public static Sprite GetSprite(int radius)
    {
        if (_cache.TryGetValue(radius, out var cached) && cached != null)
            return cached;

        var sprite = BuildSprite(radius);
        _cache[radius] = sprite;
        return sprite;
    }

    static Sprite BuildSprite(int radius)
    {
        // Texture must be at least radius*2 + 1 in each dimension
        // We use radius*2 + 2 to give the center a 2px safe zone
        int size = radius * 2 + 2;
        int w = size, h = size;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        var pixels = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                pixels[y * w + x] = IsInside(x, y, w, h, radius)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // Border = radius pixels on each side — this is the key for correct 9-slicing
        var border = new Vector4(radius, radius, radius, radius);
        // pixelsPerUnit must match so 9-slice borders render at correct size
        // Setting it to 1 means 1 pixel = 1 unit, border=radius pixels = radius units
        return Sprite.Create(tex,
            new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.5f),
            1f,
            0,
            SpriteMeshType.FullRect,
            border);
    }

    static bool IsInside(int x, int y, int w, int h, int r)
    {
        // Corner check — only corners need radius test
        int cx = x < r       ? r - x       : (x >= w - r ? x - (w - r) + 1 : 0);
        int cy = y < r       ? r - y       : (y >= h - r ? y - (h - r) + 1 : 0);

        if (cx > 0 && cy > 0)
        {
            // In a corner zone — check circle
            float dx = cx - 0.5f;
            float dy = cy - 0.5f;
            return dx * dx + dy * dy <= (float)(r * r);
        }
        return true;
    }

    public static void ClearCache() => _cache.Clear();
}